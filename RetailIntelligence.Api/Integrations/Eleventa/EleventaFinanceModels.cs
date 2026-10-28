namespace RetailIntelligence.Api.Integrations.Eleventa;

public sealed class EleventaFinanceEntry
{
    public Guid SourceId { get; set; }
    public Guid EntryId { get; set; }
    public string Kind { get; set; } = "";
    public DateOnly OccurredOn { get; set; }
    public decimal Amount { get; set; }
    public string Category { get; set; } = "";
    public string Description { get; set; } = "";
    public string Counterparty { get; set; } = "";
    public string Reference { get; set; } = "";
    public string Notes { get; set; } = "";
    public DateTime CreatedAt { get; set; }
    public DateTime? VoidedAt { get; set; }
    public string? VoidReason { get; set; }
}

public sealed record FinanceEntryRequest(Guid EntryId, string Kind, DateOnly OccurredOn,
    decimal Amount, string Category, string Description, string Counterparty = "", string Reference = "", string Notes = "");
public sealed record FinanceVoidRequest(string Reason);
public sealed record FinanceEntryView(Guid EntryId, string Kind, DateOnly OccurredOn, decimal Amount,
    string Category, string Description, string Counterparty, string Reference, string Notes,
    DateTime CreatedAtUtc, DateTime? VoidedAtUtc, string? VoidReason);
public sealed record FinanceEntryPage(int Skip, int Take, int Total, IReadOnlyList<FinanceEntryView> Entries);
public sealed record FinanceCategoryTotal(string Category, decimal Amount);
public sealed record FinanceSummary(DateOnly From, DateOnly To, int TicketCount, decimal Sales,
    decimal Returns, decimal NetSales, decimal? TicketMargin, decimal Purchases, decimal SupplierPayments,
    decimal Expenses, decimal OwnerWithdrawals, decimal StockWithdrawals, decimal CashIn, decimal CashOut,
    decimal RecordedCashFlow, decimal? OpeningInventory, decimal? ClosingInventory, decimal? MerchandiseCost,
    decimal? EstimatedProfit, string ProfitBasis, DateTime? LastSyncAtUtc,
    EleventaFinanceClosing? Opening, EleventaFinanceClosing? Closing,
    IReadOnlyList<FinanceCategoryTotal> ExpensesByCategory, IReadOnlyList<string> Warnings);
public sealed record EleventaFinanceClosing(DateOnly Date, string Status, DateTimeOffset? CapturedAtUtc, decimal? Value);
