using Eleventa.Agent.Configuration;
using Eleventa.Agent.Services;
using Eleventa.Agent.Storage;
using Microsoft.Extensions.Logging.EventLog;
using Microsoft.Extensions.Logging.Configuration;

var builder = Host.CreateApplicationBuilder(args);
var configuredStateDirectory = Environment.ExpandEnvironmentVariables(
    builder.Configuration["Integration:StateDirectory"] ?? "%ProgramData%\\RetailIntelligence\\EleventaAgent");
if (!args.Contains("--probe-firebird", StringComparer.OrdinalIgnoreCase) &&
    !args.Contains("--probe-extraction", StringComparer.OrdinalIgnoreCase))
{
    builder.Configuration.AddJsonFile(Path.Combine(configuredStateDirectory, "agent.settings.json"), optional: true, reloadOnChange: false);
    builder.Configuration.AddEnvironmentVariables();
}
var options = builder.Configuration.Get<AgentOptions>() ?? new AgentOptions();
options.Integration.StateDirectory = Environment.ExpandEnvironmentVariables(options.Integration.StateDirectory);
if (args.Contains("--probe-firebird", StringComparer.OrdinalIgnoreCase) || args.Contains("--probe-extraction", StringComparer.OrdinalIgnoreCase))
{
    var logger = LoggerFactory.Create(logging => logging.AddConsole()).CreateLogger<FirebirdExtractor>();
    try
    {
        var extractor = new FirebirdExtractor(options, logger);
        if (args.Contains("--probe-extraction", StringComparer.OrdinalIgnoreCase))
        {
            if (options.Integration.SourceId == Guid.Empty) options.Integration.SourceId = Guid.Parse("11111111-1111-1111-1111-111111111111");
            var batches = await extractor.ExtractAsync(0, 0,
                DateTime.Now.AddDays(-Math.Clamp(options.Integration.SalesLookbackDays, 1, 3650)),
                DateTime.Now.AddDays(-Math.Clamp(options.Integration.ReturnsLookbackDays, 1, 3650)),
                true, true, CancellationToken.None);
            Console.WriteLine($"Extracción Firebird READ ONLY correcta: {string.Join(", ", batches.Select(x => $"{x.Batch.Kind}={x.Batch.Products.Count + (x.Batch.Inventory?.Rows.Count ?? 0) + x.Batch.Sales.Sum(s => 1 + s.Lines.Count) + x.Batch.Returns.Sum(s => 1 + s.Lines.Count)} registros"))}.");
        }
        else
        {
            var count = await extractor.ProbeReadOnlyAsync(CancellationToken.None);
            Console.WriteLine($"Firebird respondió en transacción READ ONLY. PRODUCTOS: {count} filas.");
        }
        return;
    }
    catch
    {
        Console.Error.WriteLine("No se pudo completar la prueba Firebird de solo lectura. Revisa configuración y versión del servidor; no se imprimen credenciales.");
        Environment.ExitCode = 2;
        return;
    }
}
if (args.Contains("--requeue-deadletters", StringComparer.OrdinalIgnoreCase))
{
    Directory.CreateDirectory(options.Integration.StateDirectory);
    using var maintenanceLock = new FileStream(Path.Combine(options.Integration.StateDirectory, "agent.lock"),
        FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
    var store = new OutboxStore(Path.Combine(options.Integration.StateDirectory, "sync-state.json"));
    await store.InitializeAsync(CancellationToken.None);
    var count = await store.RequeueDeadLettersAsync(CancellationToken.None);
    Console.WriteLine($"Se reactivaron {count} lotes fallidos para reintentar.");
    return;
}
builder.Services.AddSingleton(options);
builder.Services.AddWindowsService(settings => settings.ServiceName = "Retail Intelligence Eleventa Sync");
LoggerProviderOptions.RegisterProviderOptions<EventLogSettings, EventLogLoggerProvider>(builder.Services);
builder.Logging.AddEventLog(settings => settings.SourceName = "Retail Intelligence Eleventa Sync");
builder.Logging.AddProvider(new FileLogProvider(Path.Combine(options.Integration.StateDirectory, "logs")));
builder.Services.AddSingleton(new OutboxStore(Path.Combine(options.Integration.StateDirectory, "sync-state.json")));
builder.Services.AddSingleton<FirebirdExtractor>();
builder.Services.AddHttpClient<SyncWorker>(client =>
{
    if (Uri.TryCreate(options.Integration.ApiBaseUrl, UriKind.Absolute, out var address) && address.Scheme == Uri.UriSchemeHttps)
        client.BaseAddress = address;
    client.Timeout = TimeSpan.FromSeconds(60);
}).ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false });
Directory.CreateDirectory(options.Integration.StateDirectory);
using var singleInstanceLock = new FileStream(Path.Combine(options.Integration.StateDirectory, "agent.lock"),
    FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
builder.Services.AddHostedService(provider => provider.GetRequiredService<SyncWorker>());

await builder.Build().RunAsync();
