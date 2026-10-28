using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using RetailIntelligence.Api.Data;
using RetailIntelligence.Api.Integrations.Eleventa;
using RetailIntelligence.Contracts.Eleventa;

internal static class EleventaFinanceTests
{
    public static void Validation()
    {
        var today = new DateOnly(2026, 12, 1);
        var valid = new FinanceEntryRequest(Guid.NewGuid(), "expense", today, 1.25m, "services", "Electricidad");
        EleventaFinanceService.Validate(valid, today);
        void Reject(FinanceEntryRequest request)
        {
            try { EleventaFinanceService.Validate(request, today); }
            catch (ArgumentException) { return; }
            throw new InvalidOperationException("Finance validation accepted an invalid request.");
        }
        Reject(valid with { Amount = 0 }); Reject(valid with { Amount = 1.001m }); Reject(valid with { Amount = 1000000000m });
        Reject(valid with { Kind = "unknown" }); Reject(valid with { OccurredOn = today.AddDays(1) });
        Reject(valid with { Description = " " }); Reject(valid with { EntryId = Guid.Empty });
        Reject(valid with { Category = "personal" }); Reject(valid with { Kind = "purchase", Category = "services" });
        Console.WriteLine("PASS: finance amount, date, category, identity and description validation.");
    }

    public static async Task RunAsync(AppDbContext db)
    {
        var source = Guid.NewGuid(); var other = Guid.NewGuid();
        var now = new DateTimeOffset(2026, 12, 1, 20, 0, 0, TimeSpan.Zero);
        foreach (var id in new[] { source, other }) db.EleventaSources.Add(new()
        {
            SourceId = id, Name = id == source ? "Finance tests" : "Other finance source", IsActive = true, CreatedAt = now.UtcDateTime,
            LastConfirmedAt = now.UtcDateTime, CredentialSha256 = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(id.ToString())))
        });
        await db.SaveChangesAsync();
        var sync = new EleventaSyncService(db); var service = new EleventaFinanceService(db, sync);
        var from = new DateOnly(2026, 11, 1); var to = new DateOnly(2026, 11, 30); var day = new DateOnly(2026, 11, 15);
        async Task Snapshot(DateOnly date, decimal quantity)
        {
            await sync.AcceptAsync(new EleventaBatchRequest
            {
                SourceId = source, BatchId = Guid.NewGuid(), Kind = "inventory", CreatedAtUtc = now,
                Inventory = new() { SnapshotId = Guid.NewGuid(), ExtractedAtUtc = EleventaClosingSchedule.ScheduledAt(date), ExpectedRecordCount = 1, IsFinalChunk = true,
                    Rows = [new() { SourceProductId = 1, SourceWarehouseId = 1, Quantity = quantity, UnitCost = 100m }] }
            }, source, default);
        }
        await Snapshot(from.AddDays(-1), 1000m); await Snapshot(to, 1050m);
        db.EleventaSales.Add(new() { SourceId = source, SourceTicketId = 1, SoldAt = day.ToDateTime(new TimeOnly(12, 0)), Total = 41000m, Profit = 16000m, ObservedAt = now.UtcDateTime });
        db.EleventaSales.Add(new() { SourceId = source, SourceTicketId = 2, SoldAt = day.ToDateTime(new TimeOnly(12, 0)), Total = 9999m, Profit = 9999m, IsCancelled = true, ObservedAt = now.UtcDateTime });
        db.EleventaSales.Add(new() { SourceId = source, SourceTicketId = 3, SoldAt = day.ToDateTime(new TimeOnly(12, 0)), Total = 9999m, Profit = 9999m, IsOpen = true, ObservedAt = now.UtcDateTime });
        db.EleventaReturns.Add(new() { SourceId = source, SourceReturnId = 1, ReturnedAt = day.ToDateTime(new TimeOnly(13, 0)), Amount = 1000m, ObservedAt = now.UtcDateTime });
        await db.SaveChangesAsync();
        FinanceEntryRequest Request(string kind, decimal amount, DateOnly? date = null) => new(Guid.NewGuid(), kind, date ?? day,
            amount, kind == "expense" ? "payroll" : "", "Prueba " + kind, "Proveedor de prueba", "Referencia", "Datos ficticios");
        var purchase = Request("purchase", 32000m);
        await service.CreateAsync(source, purchase, now, default); await service.CreateAsync(source, purchase, now, default);
        Check(await db.EleventaFinanceEntries.CountAsync(e => e.SourceId == source && e.EntryId == purchase.EntryId) == 1, "idempotent create");
        try { await service.CreateAsync(source, purchase with { Amount = 33000m }, now, default); throw new InvalidOperationException("Conflicting retry accepted."); }
        catch (EleventaBatchConflictException) { }
        await service.CreateAsync(source, Request("purchase-return", 2000m), now, default);
        await service.CreateAsync(source, Request("supplier-payment", 28000m), now, default);
        await service.CreateAsync(source, Request("supplier-refund", 400m), now, default);
        var expense = Request("expense", 8000m);
        await service.CreateAsync(source, expense, now, default);
        await service.CreateAsync(source, Request("owner-withdrawal", 3000m), now, default);
        await service.CreateAsync(source, Request("owner-contribution", 2000m), now, default);
        await service.CreateAsync(source, Request("customer-collection", 41000m), now, default);
        await service.CreateAsync(source, Request("customer-refund", 1000m), now, default);
        await service.CreateAsync(source, Request("expense", 321m, from.AddDays(-2)), now, default);
        await service.CreateAsync(other, purchase with { Amount = 77m }, now, default);
        var summary = await service.SummaryAsync(source, from, to, now, default);
        Check(summary.NetSales == 40000m && summary.TicketCount == 1 && summary.TicketMargin is null, "sales states and return treatment");
        Check(summary.Purchases == 30000m && summary.MerchandiseCost == 25000m && summary.EstimatedProfit == 7000m && summary.ProfitBasis == "inventory", "periodic inventory profit without double-counting supplier payments");
        Check(summary.CashIn == 43400m && summary.CashOut == 40000m && summary.RecordedCashFlow == 3400m, "cash flow excludes purchases and sales themselves");
        Check(summary.ExpensesByCategory.Single().Amount == 8000m && summary.OwnerWithdrawals == 3000m, "personal withdrawals separated from expenses");
        var page = await service.ListAsync(other, from, to, 0, 50, default);
        Check(page.Total == 1 && page.Entries.Single().Amount == 77m, "source isolation with same entry id");
        Check(await service.VoidAsync(Guid.NewGuid(), expense.EntryId, "No pertenece a este origen", now, default) is null, "cannot void another source's entry");
        await service.CreateAsync(source, Request("stock-withdrawal", 1000m), now, default);
        summary = await service.SummaryAsync(source, from, to, now, default);
        Check(summary.MerchandiseCost == 24000m && summary.EstimatedProfit == 8000m && summary.RecordedCashFlow == 3400m, "personal inventory usage is not a sale or cash payment");
        await service.VoidAsync(source, expense.EntryId, "Importe incorrecto", now, default);
        var voided = await service.VoidAsync(source, expense.EntryId, "Segundo intento", now.AddMinutes(1), default);
        Check(voided?.VoidReason == "Importe incorrecto" && voided.Amount == 8000m, "void preserves original data and first reason");
        summary = await service.SummaryAsync(source, from, to, now, default);
        Check(summary.Expenses == 0m && summary.EstimatedProfit == 16000m && summary.RecordedCashFlow == 11400m, "voided entries are excluded from totals");
        page = await service.ListAsync(source, from, to, 0, 2, default);
        Check(page.Entries.Count == 2 && page.Total > 2, "pagination includes historical voided records");
        var impossible = Request("purchase-return", 50000m);
        await service.CreateAsync(source, impossible, now, default);
        summary = await service.SummaryAsync(source, from, to, now, default);
        Check(summary.MerchandiseCost < 0 && summary.EstimatedProfit is null, "invalid inventory arithmetic is not a profit");
        await service.VoidAsync(source, impossible.EntryId, "Solo prueba de conciliación", now, default);
        var december = new DateOnly(2026, 12, 1);
        db.EleventaSales.Add(new() { SourceId = source, SourceTicketId = 4, SoldAt = december.ToDateTime(new TimeOnly(10, 0)), Total = 100m, Profit = 25m, ObservedAt = now.UtcDateTime });
        await db.SaveChangesAsync();
        await service.CreateAsync(source, Request("expense", 5m, december), now, default);
        summary = await service.SummaryAsync(source, december, december, now, default);
        Check(summary.ProfitBasis == "tickets" && summary.EstimatedProfit == 20m && summary.RecordedCashFlow == -5m, "provisional margin does not count sales as collections");
        db.EleventaReturns.Add(new() { SourceId = source, SourceReturnId = 2, ReturnedAt = december.ToDateTime(new TimeOnly(11, 0)), Amount = 5m, ObservedAt = now.UtcDateTime });
        await db.SaveChangesAsync();
        summary = await service.SummaryAsync(source, december, december, now, default);
        Check(summary.EstimatedProfit is null && summary.ProfitBasis == "unavailable", "unallocated return margin is not invented");
        Console.WriteLine("PASS: finance idempotency, source isolation, purchase/payment separation, personal withdrawals, cash flow, returns, void history, pagination and profit availability.");
    }

    private static void Check(bool condition, string name)
    { if (!condition) throw new InvalidOperationException("Finance check failed: " + name); }
}
