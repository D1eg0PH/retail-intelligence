using RetailIntelligence.Api.Data;
using RetailIntelligence.Api.Integrations.Eleventa;
using RetailIntelligence.Contracts.Eleventa;

internal static class EleventaInventoryHistoryTests
{
    public static async Task RunAsync(AppDbContext db)
    {
        var source = Guid.NewGuid();
        db.EleventaSources.Add(new EleventaSource { SourceId = source, Name = "Historical cost test", CredentialSha256 = new string('0', 64), IsActive = true, CreatedAt = DateTime.UtcNow });
        await db.SaveChangesAsync();
        var service = new EleventaSyncService(db);
        var timestamp = DateTimeOffset.UtcNow.AddDays(-2);
        EleventaBatchRequest Inventory(decimal? cost, DateTimeOffset at) => new()
        {
            SourceId = source, BatchId = Guid.NewGuid(), Kind = "inventory", CreatedAtUtc = at,
            Inventory = new() { SnapshotId = Guid.NewGuid(), ExtractedAtUtc = at, ExpectedRecordCount = 1, IsFinalChunk = true,
                Rows = [new() { SourceProductId = 1, SourceWarehouseId = 1, Quantity = 10m, UnitCost = cost }] }
        };
        var first = Inventory(20m, timestamp);
        await service.AcceptAsync(first, source, default);
        var retry = await service.AcceptAsync(first, source, default);
        if (!retry.Duplicate) throw new InvalidOperationException("Inventory retry must be idempotent.");
        await service.AcceptAsync(Inventory(25m, timestamp.AddDays(1)), source, default);
        // Current catalog changes must never change an earlier snapshot's value.
        db.EleventaProducts.Add(new EleventaProduct { SourceId = source, SourceProductId = 1, CodeOriginal = "test", DescriptionOriginal = "test", Cost = 99m, ObservedAt = DateTime.UtcNow });
        await db.SaveChangesAsync();
        var legacy = Inventory(null, timestamp.AddDays(2));
        await service.AcceptAsync(legacy, source, default);
        var partial = Inventory(30m, timestamp.AddHours(1));
        partial.Inventory!.IsFinalChunk = false;
        await service.AcceptAsync(partial, source, default);
        var history = await service.GetInventoryHistoryAsync(source, 0, 30, default);
        if (history.Total != 3 || history.Snapshots.Count != 3 ||
            history.Snapshots.Single(s => s.SnapshotId == first.Inventory!.SnapshotId).TotalCostValue != 200m ||
            !history.Snapshots.Any(s => s.TotalCostValue == 250m) ||
            history.Snapshots.Single(s => s.SnapshotId == legacy.Inventory!.SnapshotId).TotalCostValue is not null ||
            history.Snapshots.Single(s => s.SnapshotId == legacy.Inventory!.SnapshotId).RowsWithoutCost != 1)
            throw new InvalidOperationException("Historical values, missing costs or incomplete snapshot filtering failed.");
        var other = await service.GetInventoryHistoryAsync(Guid.NewGuid(), 0, 30, default);
        if (other.Total != 0) throw new InvalidOperationException("Inventory history leaked another source.");
        var page = await service.GetInventoryHistoryAsync(source, 1, 1, default);
        if (page.Total != 3 || page.Snapshots.Count != 1) throw new InvalidOperationException("History pagination failed.");
        Console.WriteLine("PASS: immutable inventory valuation, legacy costs, completeness, source isolation and pagination.");
    }
}
