using Microsoft.EntityFrameworkCore;
using Npgsql;
using RetailIntelligence.Api.Data;
using RetailIntelligence.Contracts.Eleventa;

namespace RetailIntelligence.Api.Integrations.Eleventa;

public sealed class EleventaFinanceService(AppDbContext db, EleventaSyncService sync)
{
    public static readonly string[] Kinds = ["purchase", "purchase-return", "supplier-payment", "supplier-refund",
        "expense", "owner-withdrawal", "owner-contribution", "customer-collection", "customer-refund", "stock-withdrawal"];
    public static readonly string[] Categories = ["payroll", "services", "rent", "transport", "maintenance", "other"];

    public static void Validate(FinanceEntryRequest r, DateOnly today)
    {
        if (r.EntryId == Guid.Empty || !Kinds.Contains(r.Kind) || r.OccurredOn < new DateOnly(2000, 1, 1) || r.OccurredOn > today ||
            r.Amount <= 0 || r.Amount > 999999999.99m || decimal.Round(r.Amount, 2) != r.Amount ||
            string.IsNullOrWhiteSpace(r.Description) || r.Description.Length > 250 ||
            r.Counterparty is null || r.Counterparty.Length > 150 || r.Reference is null || r.Reference.Length > 100 ||
            r.Notes is null || r.Notes.Length > 1000 || r.Category is null ||
            (r.Kind == "expense" ? !Categories.Contains(r.Category) : r.Category != ""))
            throw new ArgumentException("Revisa tipo, fecha, importe (máximo dos decimales), categoría y descripción del movimiento.");
    }

    public async Task<FinanceEntryView> CreateAsync(Guid source, FinanceEntryRequest request, DateTimeOffset now, CancellationToken ct)
    {
        Validate(request, DateOnly.FromDateTime(EleventaClosingSchedule.LocalTime(now)));
        var existing = await db.EleventaFinanceEntries.AsNoTracking().SingleOrDefaultAsync(e => e.SourceId == source && e.EntryId == request.EntryId, ct);
        if (existing is not null) return MatchOrConflict(existing, request);
        var entry = new EleventaFinanceEntry
        {
            SourceId = source, EntryId = request.EntryId, Kind = request.Kind, OccurredOn = request.OccurredOn,
            Amount = request.Amount, Category = request.Category, Description = request.Description.Trim(),
            Counterparty = request.Counterparty.Trim(), Reference = request.Reference.Trim(), Notes = request.Notes.Trim(), CreatedAt = now.UtcDateTime
        };
        db.EleventaFinanceEntries.Add(entry);
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            db.Entry(entry).State = EntityState.Detached;
            var saved = await db.EleventaFinanceEntries.AsNoTracking().SingleAsync(e => e.SourceId == source && e.EntryId == request.EntryId, ct);
            return MatchOrConflict(saved, request);
        }
        return View(entry);
    }

    private static FinanceEntryView MatchOrConflict(EleventaFinanceEntry e, FinanceEntryRequest r)
    {
        if (e.Kind != r.Kind || e.OccurredOn != r.OccurredOn || e.Amount != r.Amount || e.Category != r.Category ||
            e.Description != r.Description.Trim() || e.Counterparty != r.Counterparty.Trim() || e.Reference != r.Reference.Trim() || e.Notes != r.Notes.Trim())
            throw new EleventaBatchConflictException("Este movimiento ya se guardó con otro contenido. Actualiza la lista antes de registrar uno nuevo.");
        return View(e);
    }

    public async Task<FinanceEntryPage> ListAsync(Guid source, DateOnly from, DateOnly to, int skip, int take, CancellationToken ct)
    {
        var query = db.EleventaFinanceEntries.AsNoTracking().Where(e => e.SourceId == source && e.OccurredOn >= from && e.OccurredOn <= to);
        var count = await query.CountAsync(ct);
        var rows = await query.OrderByDescending(e => e.OccurredOn).ThenByDescending(e => e.CreatedAt).ThenBy(e => e.EntryId)
            .Skip(skip).Take(take).ToListAsync(ct);
        return new(skip, take, count, rows.Select(View).ToList());
    }

    public async Task<FinanceEntryView?> VoidAsync(Guid source, Guid id, string reason, DateTimeOffset now, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(reason) || reason.Length > 300) throw new ArgumentException("Escribe un motivo de anulación de hasta 300 caracteres.");
        await db.EleventaFinanceEntries.Where(e => e.SourceId == source && e.EntryId == id && e.VoidedAt == null)
            .ExecuteUpdateAsync(set => set.SetProperty(e => e.VoidedAt, now.UtcDateTime).SetProperty(e => e.VoidReason, reason.Trim()), ct);
        var entry = await db.EleventaFinanceEntries.AsNoTracking().SingleOrDefaultAsync(e => e.SourceId == source && e.EntryId == id, ct);
        return entry is null ? null : View(entry);
    }

    public async Task<FinanceSummary> SummaryAsync(Guid source, DateOnly from, DateOnly to, DateTimeOffset now, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.RepeatableRead, ct);
        var entries = await db.EleventaFinanceEntries.AsNoTracking()
            .Where(e => e.SourceId == source && e.VoidedAt == null && e.OccurredOn >= from && e.OccurredOn <= to).ToListAsync(ct);
        decimal Total(params string[] kinds) => entries.Where(e => kinds.Contains(e.Kind)).Sum(e => e.Amount);
        var beginLocal = from.ToDateTime(TimeOnly.MinValue);
        var endLocal = to.AddDays(1).ToDateTime(TimeOnly.MinValue);
        var sales = await db.EleventaSales.AsNoTracking().Where(s => s.SourceId == source && !s.IsOpen && !s.IsCancelled &&
            s.SoldAt >= beginLocal && s.SoldAt < endLocal).Select(s => new { s.Total, s.Profit, s.SoldAt }).ToListAsync(ct);
        var returns = await db.EleventaReturns.AsNoTracking().Where(r => r.SourceId == source &&
            (r.ReturnedAt ?? r.PaidAt) >= beginLocal && (r.ReturnedAt ?? r.PaidAt) < endLocal).SumAsync(r => r.Amount, ct);
        var undatedReturns = await db.EleventaReturns.AnyAsync(r => r.SourceId == source && r.ReturnedAt == null && r.PaidAt == null, ct);
        var gross = sales.Sum(s => s.Total ?? 0m);
        var net = gross - returns;
        var purchases = Total("purchase") - Total("purchase-return");
        var expenses = Total("expense");
        var withdrawals = Total("owner-withdrawal");
        var stockWithdrawals = Total("stock-withdrawal");
        var cashIn = Total("customer-collection", "supplier-refund", "owner-contribution");
        var cashOut = Total("supplier-payment", "expense", "owner-withdrawal", "customer-refund");
        var warnings = new List<string>();
        // A full calendar week/month uses the preceding closing as its opening balance.
        var report = await sync.GetInventoryClosingsAsync(source, from.AddDays(-1), to, now, ct);
        EleventaFinanceClosing? Closing(DateOnly date)
        {
            var close = report.Closings.SingleOrDefault(c => c.ClosingDate == date);
            return close is null ? null : new(date, close.Status, close.Snapshot?.ExtractedAtUtc, close.Snapshot?.TotalCostValue);
        }
        var opening = Closing(from.AddDays(-1));
        var closing = Closing(to);
        var outsideClosing = false;
        if (opening?.CapturedAtUtc is { } openingAt)
        {
            var openingLocal = EleventaClosingSchedule.LocalTime(openingAt);
            outsideClosing = await db.EleventaSales.AnyAsync(s => s.SourceId == source && !s.IsOpen && !s.IsCancelled &&
                s.SoldAt > openingLocal && s.SoldAt < beginLocal, ct);
        }
        if (closing?.CapturedAtUtc is { } closingAt)
            outsideClosing |= sales.Any(s => s.SoldAt > EleventaClosingSchedule.LocalTime(closingAt));
        decimal? cost = opening?.Value is { } initial && closing?.Value is { } final ? initial + purchases - final - stockWithdrawals : null;
        var sourceRecord = await db.EleventaSources.AsNoTracking().SingleAsync(s => s.SourceId == source, ct);
        var completeSales = sales.All(s => s.Total is not null) && !undatedReturns;
        decimal? margin = sales.Count > 0 && sales.All(s => s.Profit is not null) && returns == 0 && !undatedReturns
            ? sales.Sum(s => s.Profit!.Value) : null;
        decimal? profit = null;
        var basis = "unavailable";
        if (cost is >= 0 && completeSales && !outsideClosing) { profit = net - cost.Value - expenses; basis = "inventory"; }
        else if (margin is not null && completeSales) { profit = margin - expenses; basis = "tickets"; }
        if (opening?.Value is null || closing?.Value is null) warnings.Add("Falta un cierre inicial o final con costo completo. El cálculo por inventario todavía no está disponible.");
        if (cost is < 0) warnings.Add("El costo calculado resultó negativo. Revisa compras, retiros de mercancía y cierres antes de usar esta cifra.");
        if (opening?.Status == "late" || closing?.Status == "late") warnings.Add("Hay capturas de cierre tardías; consulta sus horas reales.");
        if (outsideClosing) warnings.Add("Hay ventas entre el cierre y el límite del período. No se usa el método de inventario hasta conciliar esas ventas.");
        if (sales.Any(s => s.Total is null)) warnings.Add("Hay ventas sin importe; el resultado está incompleto.");
        if (undatedReturns) warnings.Add("Hay devoluciones sin fecha que no se pueden asignar al período.");
        if (returns > 0 && basis != "inventory") warnings.Add("No se estima la utilidad por tickets porque las devoluciones no permiten recuperar con certeza su margen.");
        if (basis == "tickets") warnings.Add("Estimación provisional: margen registrado en Eleventa menos gastos pagados. No es un cierre por inventario.");
        warnings.Add("El flujo incluye solo los cobros, pagos y aportaciones que captures; las ventas de Eleventa no se consideran cobros automáticamente.");
        warnings.Add("La utilidad depende de compras, gastos pagados, costos y existencias correctos. No sustituye el cálculo fiscal ni la conciliación de caja.");
        var summary = new FinanceSummary(from, to, sales.Count, gross, returns, net, margin, purchases, Total("supplier-payment"), expenses, withdrawals,
            stockWithdrawals, cashIn, cashOut, cashIn - cashOut, opening?.Value, closing?.Value, cost, profit, basis,
            sourceRecord.LastConfirmedAt, opening, closing,
            entries.Where(e => e.Kind == "expense").GroupBy(e => e.Category).Select(g => new FinanceCategoryTotal(g.Key, g.Sum(e => e.Amount))).OrderByDescending(g => g.Amount).ToList(), warnings);
        await transaction.CommitAsync(ct);
        return summary;
    }

    private static FinanceEntryView View(EleventaFinanceEntry e) => new(e.EntryId, e.Kind, e.OccurredOn, e.Amount, e.Category,
        e.Description, e.Counterparty, e.Reference, e.Notes, e.CreatedAt, e.VoidedAt, e.VoidReason);
}
