using Eleventa.Agent.Storage;
using RetailIntelligence.Api.Integrations.Eleventa;
using RetailIntelligence.Contracts.Eleventa;

internal static class EleventaSyncContractTests
{
    public static async Task RunAsync()
    {
        var source = Guid.NewGuid();
        ValidCatalog(source);
        ValidInventory(source);
        ValidSales(source);
        ValidReturns(source);
        RejectsWrongSource(source);
        RejectsDuplicateProduct(source);
        RejectsDuplicateInventoryRow(source);
        RejectsDuplicateSaleLines(source);
        RejectsWrongBatchShape(source);
        RejectsOversizedBatch(source);
        await VerifiesDurableOutboxAsync(source);
        await VerifiesDeadLetterRecoveryAsync(source);
        Console.WriteLine("PASS: 12 Eleventa contract and local outbox checks.");
    }

    private static void ValidCatalog(Guid source)
    {
        var batch = NewBatch(source, "catalog");
        batch.Products.Add(new EleventaProductDto { SourceProductId = 1, Code = "001-A ", Description = new string('x', 255), Cost = 2.1234m,
            SalePrice = 0m, CreatedAtOrigin = null, FirstSaleAt = new DateTime(2026, 1, 1) });
        EleventaSyncService.ValidateRequest(batch, source);
    }

    private static void ValidInventory(Guid source)
    {
        var batch = NewBatch(source, "inventory");
        batch.Inventory = new EleventaInventoryChunkDto { SnapshotId = Guid.NewGuid(), ExpectedRecordCount = 2, IsFinalChunk = false,
            Rows = [new() { SourceProductId = 1, SourceWarehouseId = 1, Quantity = -0.00000001m }] };
        EleventaSyncService.ValidateRequest(batch, source);
    }

    private static void ValidSales(Guid source)
    {
        var batch = NewBatch(source, "sales");
        batch.Sales.Add(new EleventaSaleDto { SourceTicketId = 11, Total = 20.0000m, SoldAt = new DateTime(2026, 1, 1), Currency = null,
            Lines = [new() { SourceLineId = 8, ProductCode = "00042", ProductName = "Producto", Quantity = 0.25m, LineTotal = 5.0000m }] });
        EleventaSyncService.ValidateRequest(batch, source);
    }

    private static void ValidReturns(Guid source)
    {
        var headerOnly = NewBatch(source, "returns");
        headerOnly.Returns.Add(new() { SourceReturnId = 7, SourceTicketId = null, Amount = 15.0000m, Lines = [] });
        EleventaSyncService.ValidateRequest(headerOnly, source);
        var withDetail = NewBatch(source, "returns");
        withDetail.Returns.Add(new() { SourceReturnId = 8, Amount = 4.0000m,
            Lines = [new() { SourceReturnLineId = 9, ProductCode = "001", ProductDescription = "Artículo", ReturnedQuantity = 0.5m, Amount = 4m }] });
        EleventaSyncService.ValidateRequest(withDetail, source);
    }

    private static void RejectsWrongSource(Guid source) =>
        AssertThrows(() => EleventaSyncService.ValidateRequest(NewBatch(source, "catalog"), Guid.NewGuid()), "credential source mismatch");

    private static void RejectsDuplicateProduct(Guid source)
    {
        var batch = NewBatch(source, "catalog");
        batch.Products.Add(new() { SourceProductId = 1, Code = "a", Description = "a" });
        batch.Products.Add(new() { SourceProductId = 1, Code = "b", Description = "b" });
        AssertThrows(() => EleventaSyncService.ValidateRequest(batch, source), "duplicate product identity");
    }

    private static void RejectsDuplicateInventoryRow(Guid source)
    {
        var batch = NewBatch(source, "inventory");
        batch.Inventory = new() { SnapshotId = Guid.NewGuid(), ExpectedRecordCount = 2,
            Rows = [new() { SourceProductId = 1, SourceWarehouseId = 1 }, new() { SourceProductId = 1, SourceWarehouseId = 1 }] };
        AssertThrows(() => EleventaSyncService.ValidateRequest(batch, source), "duplicate product warehouse key");
    }

    private static void RejectsDuplicateSaleLines(Guid source)
    {
        var batch = NewBatch(source, "sales");
        batch.Sales.Add(new() { SourceTicketId = 1, Lines = [new() { SourceLineId = 10 }, new() { SourceLineId = 10 }] });
        AssertThrows(() => EleventaSyncService.ValidateRequest(batch, source), "duplicate sale line identity");
    }

    private static void RejectsWrongBatchShape(Guid source)
    {
        var batch = NewBatch(source, "returns"); batch.Sales.Add(new() { SourceTicketId = 1 });
        AssertThrows(() => EleventaSyncService.ValidateRequest(batch, source), "kind and payload mismatch");
    }

    private static void RejectsOversizedBatch(Guid source)
    {
        var batch = NewBatch(source, "catalog");
        for (var id = 1; id <= 5001; id++) batch.Products.Add(new() { SourceProductId = id, Code = "c" + id, Description = "d" });
        AssertThrows(() => EleventaSyncService.ValidateRequest(batch, source), "maximum batch size");
    }

    private static async Task VerifiesDurableOutboxAsync(Guid source)
    {
        var directory = Path.Combine(Path.GetTempPath(), "eleventa-sync-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var path = Path.Combine(directory, "state.json");
            var store = new OutboxStore(path); await store.InitializeAsync(CancellationToken.None);
            var batch = new EleventaBatchRequest { ContractVersion = 1, SourceId = source, BatchId = Guid.NewGuid(), Kind = "catalog", CreatedAtUtc = DateTimeOffset.UtcNow };
            batch.Products.Add(new EleventaProductDto { SourceProductId = 1, Code = "001", Description = "Item" });
            await store.EnqueueAsync(batch, "sales-ticket-cursor", "42", CancellationToken.None);
            var restarted = new OutboxStore(path); await restarted.InitializeAsync(CancellationToken.None);
            var pending = await restarted.GetPendingAsync(CancellationToken.None);
            Assert(pending.Count == 1, "pending batch survives restart");
            await restarted.RecordAttemptAsync(pending[0], "temporary", DateTimeOffset.UtcNow.AddMinutes(1), false, CancellationToken.None);
            var retryStore = new OutboxStore(path); await retryStore.InitializeAsync(CancellationToken.None);
            var afterRetry = await retryStore.GetPendingAsync(CancellationToken.None);
            Assert(afterRetry.Single().Attempts == 1 && afterRetry.Single().NextAttemptUtc is not null, "retry state persists");
            var confirmedStore = new OutboxStore(path); await confirmedStore.InitializeAsync(CancellationToken.None);
            await confirmedStore.ConfirmAsync(afterRetry.Single(), CancellationToken.None);
            Assert((await confirmedStore.GetPendingAsync(CancellationToken.None)).Count == 0, "confirmed batch leaves outbox");
            Assert(await confirmedStore.GetCursorAsync("sales-ticket-cursor", CancellationToken.None) == 42, "cursor advances only on confirmation");
        }
        finally { Directory.Delete(directory, recursive: true); }
    }

    private static async Task VerifiesDeadLetterRecoveryAsync(Guid source)
    {
        var directory = Path.Combine(Path.GetTempPath(), "eleventa-deadletter-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var path = Path.Combine(directory, "state.json");
            var store = new OutboxStore(path); await store.InitializeAsync(CancellationToken.None);
            var batch = new EleventaBatchRequest { SourceId = source, BatchId = Guid.NewGuid(), Kind = "sales", CreatedAtUtc = DateTimeOffset.UtcNow,
                Sales = [new() { SourceTicketId = 15 }] };
            await store.EnqueueAsync(batch, "sales-scan-cursor", "15", CancellationToken.None);
            var pending = (await store.GetPendingAsync(CancellationToken.None)).Single();
            await store.RecordAttemptAsync(pending, "permanent response", null, true, CancellationToken.None);
            Assert(await store.GetCursorAsync("sales-scan-cursor", CancellationToken.None) == 15, "dead-letter lets scanner continue");
            Assert(await store.GetCursorAsync("confirmed:sales-scan-cursor", CancellationToken.None) == 0, "dead-letter does not advance confirmed cursor");
            var count = await store.RequeueDeadLettersAsync(CancellationToken.None);
            Assert(count == 1 && (await store.GetPendingAsync(CancellationToken.None)).Count == 1, "dead-letter can be requeued after repair");
        }
        finally { Directory.Delete(directory, recursive: true); }
    }

    private static EleventaBatchRequest NewBatch(Guid source, string kind) => new()
    { ContractVersion = 1, SourceId = source, BatchId = Guid.NewGuid(), Kind = kind, CreatedAtUtc = DateTimeOffset.UtcNow };

    private static void AssertThrows(Action action, string name)
    {
        try { action(); } catch (ArgumentException) { return; }
        throw new InvalidOperationException("Expected validation failure: " + name);
    }

    private static void Assert(bool condition, string name)
    { if (!condition) throw new InvalidOperationException("Assertion failed: " + name); }
}
