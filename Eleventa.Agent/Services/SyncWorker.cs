using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Eleventa.Agent.Configuration;
using Eleventa.Agent.Storage;
using RetailIntelligence.Contracts.Eleventa;

namespace Eleventa.Agent.Services;

public sealed class SyncWorker(
    AgentOptions options,
    FirebirdExtractor extractor,
    OutboxStore store,
    HttpClient httpClient,
    ILogger<SyncWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var interval = TimeSpan.FromMinutes(Math.Clamp(options.Integration.IntervalMinutes, 1, 1440));
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunCycleAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Falló el ciclo de sincronización. Se conservarán los lotes pendientes.");
                await SafeRecordRunAsync(false, "Falló un ciclo de sincronización.", false, false, stoppingToken);
            }

            try { await Task.Delay(interval, stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
        }
    }

    private async Task RunCycleAsync(CancellationToken ct)
    {
        if (options.Integration.SourceId == Guid.Empty ||
            !Uri.TryCreate(options.Integration.ApiBaseUrl, UriKind.Absolute, out var apiUri) || apiUri.Scheme != Uri.UriSchemeHttps ||
            options.Integration.ApiKey.Length < 32)
            throw new InvalidOperationException("La configuración externa de origen, URL HTTPS o credencial API está incompleta.");
        await store.InitializeAsync(ct);
        bool? firebirdReachable = null;
        bool? apiReachable = null;
        var pending = await store.GetPendingAsync(ct);
        if (pending.Count == 0)
        {
            var now = DateTimeOffset.UtcNow;
            var lastCatalog = await store.GetDateStateAsync("catalog-last-at", ct);
            var lastInventory = await store.GetDateStateAsync("inventory-last-at", ct);
            var includeCatalog = lastCatalog is null || now - lastCatalog >= TimeSpan.FromMinutes(Math.Clamp(options.Integration.CatalogIntervalMinutes, 1, 10080));
            var includeInventory = lastInventory is null || now - lastInventory >= TimeSpan.FromMinutes(Math.Clamp(options.Integration.InventoryIntervalMinutes, 1, 10080));
            var salesCursor = await store.GetCursorAsync("sales-scan-cursor", ct);
            var returnCursor = await store.GetCursorAsync("returns-scan-cursor", ct);
            var salesWindowStart = await store.GetDateStateAsync("sales-window-start", ct);
            salesWindowStart ??= DateTimeOffset.Now.AddDays(-Math.Clamp(options.Integration.SalesLookbackDays, 1, 3650));
            await store.SetStateAsync("sales-window-start", salesWindowStart.Value.ToString("O"), ct);
            var returnsWindowStart = await store.GetDateStateAsync("returns-window-start", ct);
            returnsWindowStart ??= DateTimeOffset.Now.AddDays(-Math.Clamp(options.Integration.ReturnsLookbackDays, 1, 3650));
            await store.SetStateAsync("returns-window-start", returnsWindowStart.Value.ToString("O"), ct);
            var extracted = await extractor.ExtractAsync(salesCursor, returnCursor,
                DateTime.SpecifyKind(salesWindowStart.Value.DateTime, DateTimeKind.Unspecified),
                DateTime.SpecifyKind(returnsWindowStart.Value.DateTime, DateTimeKind.Unspecified), includeCatalog, includeInventory, ct);
            firebirdReachable = true;
            foreach (var (batch, cursorKey, cursorAfter) in extracted)
            {
                if (batch.Kind == "cursor-reset")
                {
                    if (cursorKey is not null) await store.SetStateAsync(cursorKey, cursorAfter ?? "0", ct);
                    if (cursorKey == "sales-scan-cursor") await store.RemoveStateAsync("sales-window-start", ct);
                    if (cursorKey == "returns-scan-cursor") await store.RemoveStateAsync("returns-window-start", ct);
                    continue;
                }
                ValidateLocalBatch(batch);
                await store.EnqueueAsync(batch, cursorKey, cursorAfter, ct);
            }
            pending = await store.GetPendingAsync(ct);
        }

        var anyFailure = false;
        foreach (var batch in pending)
        {
            if (batch.NextAttemptUtc > DateTimeOffset.UtcNow) continue;
            var result = await SendAsync(batch, ct);
            if (result.Confirmed)
            {
                await store.ConfirmAsync(batch, ct);
                logger.LogInformation("Lote {BatchId} confirmado por la API{Duplicate}.", batch.BatchId,
                    result.Duplicate ? " (retransmisión idempotente)" : string.Empty);
                continue;
            }

            anyFailure = true;
            apiReachable = (apiReachable ?? true) && result.ApiReachable;
            var attempts = batch.Attempts + 1;
            var deadLetter = result.Permanent || attempts >= Math.Clamp(options.Integration.MaxRetries, 1, 100);
            var delay = TimeSpan.FromSeconds(Math.Min(3600, Math.Pow(2, Math.Min(attempts, 10))) + Random.Shared.Next(0, 20));
            await store.RecordAttemptAsync(batch, result.SafeError, deadLetter ? null : DateTimeOffset.UtcNow + delay, deadLetter, ct);
            if (deadLetter)
                logger.LogError("Lote {BatchId} movido a revisión local después de {Attempts} intentos: {Reason}", batch.BatchId, attempts, result.SafeError);
            else
                logger.LogWarning("No se confirmó el lote {BatchId}; reintento {Attempts} programado.", batch.BatchId, attempts);
            if (!deadLetter) break;
        }
        var remaining = await store.GetPendingAsync(ct);
        await SafeRecordRunAsync(!anyFailure && remaining.Count == 0,
            anyFailure ? "Uno o más lotes siguen pendientes." : null, apiReachable, firebirdReachable, ct);
    }

    private async Task<SendResult> SendAsync(PendingBatch batch, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "api/integrations/eleventa/v1/batches");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", options.Integration.ApiKey);
        request.Content = new StringContent(batch.Payload, System.Text.Encoding.UTF8, "application/json");
        try
        {
            using var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
            if (response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadFromJsonAsync<EleventaBatchResponse>(cancellationToken: ct);
                var expectedId = Guid.Parse(batch.BatchId);
                if (body?.BatchId != expectedId) return new(false, true, "La API respondió con una confirmación inválida.", false, true);
                return new(true, false, string.Empty, body.Duplicate, true);
            }
            var permanent = response.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden or HttpStatusCode.Conflict or HttpStatusCode.NotFound || (int)response.StatusCode == 413;
            return new(false, permanent, $"La API respondió HTTP {(int)response.StatusCode}.", false, true);
        }
        catch (HttpRequestException)
        {
            return new(false, false, "No se pudo establecer conexión HTTPS con la API.", false, false);
        }
        catch (TaskCanceledException) when (!ct.IsCancellationRequested)
        {
            return new(false, false, "La solicitud HTTPS excedió el tiempo de espera.", false, false);
        }
        catch (JsonException)
        {
            return new(false, true, "La API respondió con un cuerpo de confirmación inválido.", false, true);
        }
    }

    private async Task SafeRecordRunAsync(bool success, string? error, bool? apiReachable, bool? firebirdReachable, CancellationToken ct)
    {
        try { await store.RecordRunAsync(success, error, apiReachable, firebirdReachable, ct); } catch (Exception ex) { logger.LogWarning(ex, "No se pudo registrar el resultado local del ciclo."); }
    }

    private static void ValidateLocalBatch(EleventaBatchRequest batch)
    {
        if (batch.ContractVersion != EleventaContract.CurrentVersion || batch.SourceId == Guid.Empty || batch.BatchId == Guid.Empty)
            throw new InvalidDataException("El agente generó un contrato inválido.");
        if (batch.Products.Count > 5000 || batch.Inventory?.Rows.Count > 5000 || batch.Sales.Sum(s => 1 + s.Lines.Count) > 5000 || batch.Returns.Sum(s => 1 + s.Lines.Count) > 5000)
            throw new InvalidDataException("El agente generó un lote superior a 5000 registros.");
    }

    private sealed record SendResult(bool Confirmed, bool Permanent, string SafeError, bool Duplicate, bool ApiReachable);
}
