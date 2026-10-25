namespace RetailIntelligence.Contracts.Eleventa;

public static class EleventaContract
{
    public const int CurrentVersion = 1;
}

public sealed class EleventaBatchRequest
{
    public int ContractVersion { get; set; } = EleventaContract.CurrentVersion;
    public Guid SourceId { get; set; }
    public Guid BatchId { get; set; }
    public string Kind { get; set; } = string.Empty;
    public DateTimeOffset CreatedAtUtc { get; set; }
    public string? CursorAfter { get; set; }
    public List<EleventaProductDto> Products { get; set; } = [];
    public EleventaInventoryChunkDto? Inventory { get; set; }
    public List<EleventaSaleDto> Sales { get; set; } = [];
    public List<EleventaReturnDto> Returns { get; set; } = [];
}

public sealed class EleventaProductDto
{
    public int SourceProductId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal? Cost { get; set; }
    public decimal? SalePrice { get; set; }
    public decimal? WholesalePrice { get; set; }
    public int? DepartmentId { get; set; }
    public string? DepartmentName { get; set; }
    public int? SupplierId { get; set; }
    public string? SupplierName { get; set; }
    public DateTime? CheckedAt { get; set; }
    public DateTime? DeletedAt { get; set; }
    public bool IsKit { get; set; }
    public bool TracksInventory { get; set; }
    public DateTime? CreatedAtOrigin { get; set; }
    public string? CreatedAtSource { get; set; }
    public string? CreatedAtConfidence { get; set; }
    public DateTime? FirstSaleAt { get; set; }
    public DateTime? FirstInventoryMovementAt { get; set; }
}

public sealed class EleventaInventoryChunkDto
{
    public Guid SnapshotId { get; set; }
    public DateTimeOffset ExtractedAtUtc { get; set; }
    public int ExpectedRecordCount { get; set; }
    public bool IsFinalChunk { get; set; }
    public List<EleventaInventoryRowDto> Rows { get; set; } = [];
}

public sealed class EleventaInventoryRowDto
{
    public int SourceProductId { get; set; }
    public int SourceWarehouseId { get; set; }
    public decimal Quantity { get; set; }
}

public sealed class EleventaSaleDto
{
    public int SourceTicketId { get; set; }
    public int? Folio { get; set; }
    public DateTime? CreatedAtOrigin { get; set; }
    public DateTime? SoldAt { get; set; }
    public DateTime? PaidAt { get; set; }
    public decimal? Total { get; set; }
    public decimal? Profit { get; set; }
    public bool IsOpen { get; set; }
    public bool IsCancelled { get; set; }
    public string? Currency { get; set; }
    public List<EleventaSaleLineDto> Lines { get; set; } = [];
}

public sealed class EleventaSaleLineDto
{
    public int SourceLineId { get; set; }
    public int? SourceProductId { get; set; }
    public string ProductCode { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public decimal ReturnedQuantity { get; set; }
    public decimal? UsedPrice { get; set; }
    public decimal? FinalPrice { get; set; }
    public decimal? LineTotal { get; set; }
    public DateTime? AddedAt { get; set; }
    public bool IsReturned { get; set; }
}

public sealed class EleventaReturnDto
{
    public int SourceReturnId { get; set; }
    public int? SourceTicketId { get; set; }
    public DateTime? ReturnedAt { get; set; }
    public DateTime? PaidAt { get; set; }
    public string? ReturnType { get; set; }
    public string? Cashier { get; set; }
    public string? RegisterName { get; set; }
    public decimal Amount { get; set; }
    public List<EleventaReturnLineDto> Lines { get; set; } = [];
}

public sealed class EleventaReturnLineDto
{
    public int SourceReturnLineId { get; set; }
    public string ProductCode { get; set; } = string.Empty;
    public string ProductDescription { get; set; } = string.Empty;
    public decimal ReturnedQuantity { get; set; }
    public decimal Amount { get; set; }
}

public sealed record EleventaBatchResponse(
    Guid BatchId,
    bool Duplicate,
    int RecordCount,
    DateTimeOffset AcceptedAtUtc,
    string? CursorAfter);

public sealed record EleventaSyncStatusResponse(
    Guid SourceId,
    DateTimeOffset? LastConfirmedAtUtc,
    long AcceptedBatches,
    int LatestCompleteInventoryRows,
    DateTimeOffset? LatestCompleteInventoryAtUtc);

public sealed record EleventaProductViewDto(
    int SourceProductId, string Code, string Description, decimal? Cost, decimal? SalePrice,
    decimal? WholesalePrice, int? DepartmentId, string? DepartmentName, int? SupplierId, string? SupplierName, DateTime? DeletedAt,
    bool TracksInventory, DateTime? CreatedAtOrigin, string? CreatedAtSource, string? CreatedAtConfidence,
    DateTime? FirstSaleAt, DateTime? FirstInventoryMovementAt, DateTime ObservedAt);

public sealed record EleventaProductPageResponse(int Skip, int Take, int Total, IReadOnlyList<EleventaProductViewDto> Products);

public sealed record EleventaDashboardProductDto(
    int SourceProductId, string Code, string Description, decimal Quantity,
    decimal? Cost, decimal? SalePrice, DateTime? LastSoldAt);

public sealed record EleventaDashboardResponse(
    DateTimeOffset? InventoryAtUtc, int ProductCount, decimal TotalUnits,
    decimal InventoryCostValue, int SalesLast30Days, decimal SalesValueLast30Days,
    int Skip, int Take, int TotalProducts, IReadOnlyList<EleventaDashboardProductDto> Products);

public sealed record EleventaCleanupProductDto(
    int SourceProductId, int? DepartmentId, string Code, string Description, decimal? Quantity, decimal? Cost,
    decimal? SalePrice, DateTime? LastSoldAt, int? DaysSinceSale, string Classification,
    decimal? InventoryCostValue, bool IsDeleted);

public sealed record EleventaCleanupDepartmentDto(int DepartmentId, int ProductCount);

public sealed record EleventaProductCleanupResponse(
    DateTimeOffset? InventoryAtUtc, DateTime? SalesThrough, int InactiveDaysThreshold,
    int TotalProducts, int CandidateCount, decimal CandidateInventoryCostValue,
    int RecentMovementCount, int NoSalesObservedCount, int NoInventoryCount,
    int DeletedCount, int MissingInventoryCount, int Skip, int Take, int FilteredCount,
    IReadOnlyList<EleventaCleanupDepartmentDto> Departments,
    IReadOnlyList<EleventaCleanupProductDto> Products);

public sealed record EleventaOrderSuggestionsResponse(
    DateTimeOffset? InventoryAtUtc, DateTime SalesWindowStart, DateTime? SalesThrough,
    short WeeksAverage, short TargetInventoryDays, decimal BufferPercent,
    int ProductsWithSuggestions, decimal EstimatedCost,
    IReadOnlyList<EleventaOrderSuggestionDto> Products);

public sealed record EleventaOrderSuggestionDto(
    int SourceProductId, string Code, string Description, int? DepartmentId, string? DepartmentName, int? SupplierId, string? SupplierName,
    decimal SalesInPeriod, decimal AverageDailySales, decimal CurrentInventory,
    decimal TargetQuantity, decimal SuggestedQuantity, decimal? Cost, decimal? EstimatedCost,
    DateTime? LastSoldAt);
