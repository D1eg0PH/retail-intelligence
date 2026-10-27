namespace RetailIntelligence.Api.Integrations.Eleventa;

public sealed class EleventaSource
{
    public Guid SourceId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string CredentialSha256 { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? LastConfirmedAt { get; set; }
}

public sealed class EleventaSyncBatch
{
    public long Id { get; set; }
    public Guid SourceId { get; set; }
    public Guid BatchId { get; set; }
    public short ContractVersion { get; set; }
    public string BatchKind { get; set; } = string.Empty;
    public string PayloadSha256 { get; set; } = string.Empty;
    public int RecordCount { get; set; }
    public DateTime AcceptedAt { get; set; }
}

public sealed class EleventaProduct
{
    public Guid SourceId { get; set; }
    public int SourceProductId { get; set; }
    public string CodeOriginal { get; set; } = string.Empty;
    public string DescriptionOriginal { get; set; } = string.Empty;
    public decimal? Cost { get; set; }
    public decimal? SalePrice { get; set; }
    public decimal? WholesalePrice { get; set; }
    public int? SourceDepartmentId { get; set; }
    public string? SourceDepartmentName { get; set; }
    public int? SourceSupplierId { get; set; }
    public string? SourceSupplierName { get; set; }
    public DateTime? CheckedAt { get; set; }
    public DateTime? DeletedAt { get; set; }
    public bool IsKit { get; set; }
    public bool TracksInventory { get; set; }
    public DateTime? CreatedAtOrigin { get; set; }
    public string? CreatedAtSource { get; set; }
    public string? CreatedAtConfidence { get; set; }
    public DateTime? FirstSaleAt { get; set; }
    public DateTime? FirstInventoryMovementAt { get; set; }
    public DateTime ObservedAt { get; set; }
}

public sealed class EleventaInventorySnapshot
{
    public Guid SourceId { get; set; }
    public Guid SnapshotId { get; set; }
    public DateTime ExtractedAt { get; set; }
    public int? ExpectedRecordCount { get; set; }
    public int ReceivedRecordCount { get; set; }
    public bool IsComplete { get; set; }
    public string ValidationStatus { get; set; } = "pending";
    public DateTime? CompletedAt { get; set; }
}

public sealed class EleventaInventoryRow
{
    public Guid SourceId { get; set; }
    public Guid SnapshotId { get; set; }
    public int SourceProductId { get; set; }
    public int SourceWarehouseId { get; set; }
    public decimal Quantity { get; set; }
    public decimal? UnitCost { get; set; }
}

public sealed class EleventaSale
{
    public Guid SourceId { get; set; }
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
    public DateTime ObservedAt { get; set; }
}

public sealed class EleventaSaleLine
{
    public Guid SourceId { get; set; }
    public int SourceLineId { get; set; }
    public int SourceTicketId { get; set; }
    public int? SourceProductId { get; set; }
    public string ProductCodeOriginal { get; set; } = string.Empty;
    public string ProductNameOriginal { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public decimal ReturnedQuantity { get; set; }
    public decimal? UsedPrice { get; set; }
    public decimal? FinalPrice { get; set; }
    public decimal? LineTotal { get; set; }
    public DateTime? AddedAt { get; set; }
    public bool IsReturned { get; set; }
    public bool PresentInLatestSource { get; set; }
    public DateTime ObservedAt { get; set; }
}

public sealed class EleventaReturn
{
    public Guid SourceId { get; set; }
    public int SourceReturnId { get; set; }
    public int? SourceTicketId { get; set; }
    public DateTime? ReturnedAt { get; set; }
    public DateTime? PaidAt { get; set; }
    public string? ReturnType { get; set; }
    public string? Cashier { get; set; }
    public string? RegisterName { get; set; }
    public decimal Amount { get; set; }
    public int DetailCount { get; set; }
    public bool DetailComplete { get; set; }
    public DateTime ObservedAt { get; set; }
}

public sealed class EleventaReturnLine
{
    public Guid SourceId { get; set; }
    public int SourceReturnLineId { get; set; }
    public int SourceReturnId { get; set; }
    public int? SourceTicketId { get; set; }
    public string ProductCodeOriginal { get; set; } = string.Empty;
    public string ProductDescriptionOriginal { get; set; } = string.Empty;
    public decimal ReturnedQuantity { get; set; }
    public decimal Amount { get; set; }
}
