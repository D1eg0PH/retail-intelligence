using System.Text.Json;
using System.Security.Cryptography;
using RetailIntelligence.Contracts.Eleventa;

namespace Eleventa.Agent.Storage;

public sealed record PendingBatch(string BatchId, string Payload, string? CursorKey, string? CursorAfter, int Attempts, DateTimeOffset? NextAttemptUtc);

public sealed class OutboxStore
{
    private readonly string _path;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web);
    private StateDocument _state = new();

    public OutboxStore(string statePath)
    {
        _path = statePath;
        Directory.CreateDirectory(Path.GetDirectoryName(statePath)!);
    }

    public async Task InitializeAsync(CancellationToken ct)
    {
        await _gate.WaitAsync(ct);
        try
        {
            if (File.Exists(_path))
            {
                await using var input = File.OpenRead(_path);
                _state = await JsonSerializer.DeserializeAsync<StateDocument>(input, _json, ct)
                    ?? throw new InvalidDataException("El archivo de estado local está vacío o inválido; no se reemplazó para preservar el outbox.");
            }
            else
                await SaveAsync(ct);
        }
        finally { _gate.Release(); }
    }

    public async Task EnqueueAsync(EleventaBatchRequest batch, string? cursorKey, string? cursorAfter, CancellationToken ct)
    {
        await _gate.WaitAsync(ct);
        try
        {
            var payload = JsonSerializer.Serialize(batch, _json);
            var fingerprint = GetContentFingerprint(batch);
            if (_state.Outbox.Any(x => x.BatchId == batch.BatchId.ToString("D"))) return;
            if (_state.Outbox.Any(x => x.DeadLetter && x.ContentFingerprint == fingerprint))
            {
                // El lote ya quedó en revisión local. Se permite seguir escaneando otros IDs;
                // el cursor confirmado de servidor no se modifica.
                if (cursorKey is not null) _state.Values[cursorKey] = cursorAfter ?? "0";
                await SaveAsync(ct);
                return;
            }
            _state.Outbox.Add(new OutboxItem
            {
                BatchId = batch.BatchId.ToString("D"), Payload = payload, ContentFingerprint = fingerprint,
                CursorKey = cursorKey, CursorAfter = cursorAfter, CreatedAtUtc = DateTimeOffset.UtcNow
            });
            await SaveAsync(ct);
        }
        finally { _gate.Release(); }
    }

    public async Task<IReadOnlyList<PendingBatch>> GetPendingAsync(CancellationToken ct)
    {
        await _gate.WaitAsync(ct);
        try { return _state.Outbox.Where(x => !x.DeadLetter).OrderBy(x => x.CreatedAtUtc)
            .Select(x => new PendingBatch(x.BatchId, x.Payload, x.CursorKey, x.CursorAfter, x.Attempts, x.NextAttemptUtc)).ToArray(); }
        finally { _gate.Release(); }
    }

    public async Task ConfirmAsync(PendingBatch batch, CancellationToken ct)
    {
        await _gate.WaitAsync(ct);
        try
        {
            if (batch.CursorKey is not null)
            {
                _state.Values[batch.CursorKey] = batch.CursorAfter ?? "0";
                var confirmedKey = "confirmed:" + batch.CursorKey;
                if (long.TryParse(batch.CursorAfter, out var incoming) &&
                    long.TryParse(_state.Values.GetValueOrDefault(confirmedKey), out var current))
                    _state.Values[confirmedKey] = Math.Max(current, incoming).ToString();
                else
                    _state.Values[confirmedKey] = batch.CursorAfter ?? "0";
            }
            _state.Outbox.RemoveAll(x => x.BatchId == batch.BatchId);
            _state.LastError = null; _state.LastSuccessAtUtc = DateTimeOffset.UtcNow; _state.ApiReachable = true;
            await SaveAsync(ct);
        }
        finally { _gate.Release(); }
    }

    public async Task<int> GetCursorAsync(string key, CancellationToken ct)
    {
        await _gate.WaitAsync(ct);
        try { return _state.Values.TryGetValue(key, out var value) && int.TryParse(value, out var cursor) ? cursor : 0; }
        finally { _gate.Release(); }
    }

    public async Task SetStateAsync(string key, string value, CancellationToken ct)
    {
        await _gate.WaitAsync(ct);
        try { _state.Values[key] = value; await SaveAsync(ct); }
        finally { _gate.Release(); }
    }

    public async Task RemoveStateAsync(string key, CancellationToken ct)
    {
        await _gate.WaitAsync(ct);
        try { _state.Values.Remove(key); await SaveAsync(ct); }
        finally { _gate.Release(); }
    }

    public async Task<DateTimeOffset?> GetDateStateAsync(string key, CancellationToken ct)
    {
        await _gate.WaitAsync(ct);
        try { return _state.Values.TryGetValue(key, out var value) && DateTimeOffset.TryParse(value, out var date) ? date : null; }
        finally { _gate.Release(); }
    }

    public async Task RecordAttemptAsync(PendingBatch batch, string error, DateTimeOffset? nextAttemptUtc, bool deadLetter, CancellationToken ct)
    {
        await _gate.WaitAsync(ct);
        try
        {
            var item = _state.Outbox.Single(x => x.BatchId == batch.BatchId);
            item.Attempts++; item.NextAttemptUtc = nextAttemptUtc; item.DeadLetter = deadLetter;
            item.LastError = error.Length > 1000 ? error[..1000] : error;
            if (deadLetter && batch.CursorKey is not null)
                _state.Values[batch.CursorKey] = batch.CursorAfter ?? "0";
            _state.LastError = item.LastError;
            await SaveAsync(ct);
        }
        finally { _gate.Release(); }
    }

    public async Task RecordRunAsync(bool success, string? error, bool? apiReachable, bool? firebirdReachable, CancellationToken ct)
    {
        await _gate.WaitAsync(ct);
        try
        {
            if (apiReachable is not null) _state.ApiReachable = apiReachable;
            if (firebirdReachable is not null) _state.FirebirdReachable = firebirdReachable;
            if (error is not null) _state.LastError = error;
            if (success) _state.LastSuccessAtUtc = DateTimeOffset.UtcNow;
            await SaveAsync(ct);
        }
        finally { _gate.Release(); }
    }

    public async Task<int> RequeueDeadLettersAsync(CancellationToken ct)
    {
        await _gate.WaitAsync(ct);
        try
        {
            var requeued = 0;
            foreach (var item in _state.Outbox.Where(x => x.DeadLetter))
            {
                item.DeadLetter = false; item.Attempts = 0; item.NextAttemptUtc = null; item.LastError = null; requeued++;
            }
            if (requeued > 0) await SaveAsync(ct);
            return requeued;
        }
        finally { _gate.Release(); }
    }

    private async Task SaveAsync(CancellationToken ct)
    {
        var temporary = _path + ".tmp";
        await using (var stream = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None, 65536, FileOptions.Asynchronous | FileOptions.WriteThrough))
        {
            await JsonSerializer.SerializeAsync(stream, _state, _json, ct);
            await stream.FlushAsync(ct);
            stream.Flush(true);
        }
        File.Move(temporary, _path, true);
    }

    private string GetContentFingerprint(EleventaBatchRequest batch)
    {
        var normalized = JsonSerializer.Deserialize<EleventaBatchRequest>(JsonSerializer.SerializeToUtf8Bytes(batch, _json), _json)!;
        normalized.BatchId = Guid.Empty; normalized.CreatedAtUtc = default; normalized.CursorAfter = null;
        if (normalized.Inventory is not null)
        {
            normalized.Inventory.SnapshotId = Guid.Empty;
            normalized.Inventory.ExtractedAtUtc = default;
        }
        return Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(normalized, _json)));
    }

    private sealed class StateDocument
    {
        public Dictionary<string, string> Values { get; set; } = new(StringComparer.Ordinal);
        public List<OutboxItem> Outbox { get; set; } = [];
        public DateTimeOffset? LastSuccessAtUtc { get; set; }
        public string? LastError { get; set; }
        public bool? ApiReachable { get; set; }
        public bool? FirebirdReachable { get; set; }
    }

    private sealed class OutboxItem
    {
        public string BatchId { get; set; } = string.Empty;
        public string Payload { get; set; } = string.Empty;
        public string ContentFingerprint { get; set; } = string.Empty;
        public string? CursorKey { get; set; }
        public string? CursorAfter { get; set; }
        public int Attempts { get; set; }
        public DateTimeOffset? NextAttemptUtc { get; set; }
        public bool DeadLetter { get; set; }
        public string? LastError { get; set; }
        public DateTimeOffset CreatedAtUtc { get; set; }
    }
}
