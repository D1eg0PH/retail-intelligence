namespace Eleventa.Agent.Configuration;

public sealed class AgentOptions
{
    public FirebirdOptions Firebird { get; set; } = new();
    public IntegrationOptions Integration { get; set; } = new();
}

public sealed class FirebirdOptions
{
    public string DataSource { get; set; } = "localhost";
    public string Database { get; set; } = string.Empty;
    public int Port { get; set; } = 3050;
    public string UserId { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string Charset { get; set; } = "UTF8";
    public int Dialect { get; set; } = 3;
}

public sealed class IntegrationOptions
{
    public Guid SourceId { get; set; }
    public string ApiBaseUrl { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;
    public int IntervalMinutes { get; set; } = 5;
    public int CatalogIntervalMinutes { get; set; } = 360;
    public int InventoryIntervalMinutes { get; set; } = 60;
    public int BatchSize { get; set; } = 300;
    public int SalesLookbackDays { get; set; } = 90;
    public int ReturnsLookbackDays { get; set; } = 180;
    public int MaxRetries { get; set; } = 12;
    public string StateDirectory { get; set; } = "%ProgramData%\\RetailIntelligence\\EleventaAgent";
}
