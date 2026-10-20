using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using RetailIntelligence.Api.Data;
using RetailIntelligence.Contracts.Eleventa;

namespace RetailIntelligence.Api.Integrations.Eleventa;

public sealed class EleventaSyncService(AppDbContext db)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<EleventaBatchResponse> AcceptAsync(
        EleventaBatchRequest request, Guid authenticatedSourceId, CancellationToken cancellationToken)
    {
        ValidateRequest(request, authenticatedSourceId);
        var now = DateTime.UtcNow;
        var payloadHash = Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(request, JsonOptions)));
        var recordCount = CountRecords(request);

        var existing = await db.EleventaSyncBatches.AsNoTracking()
            .SingleOrDefaultAsync(b => b.SourceId == request.SourceId && b.BatchId == request.BatchId, cancellationToken);
        if (existing is not null)
        {
            if (!FixedHashEquals(existing.PayloadSha256, payloadHash))
                throw new EleventaBatchConflictException("El BatchId ya fue recibido con un contenido distinto.");
            return new(request.BatchId, true, existing.RecordCount, new DateTimeOffset(existing.AcceptedAt, TimeSpan.Zero), request.CursorAfter);
        }

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var source = await db.EleventaSources.SingleAsync(s => s.SourceId == authenticatedSourceId, cancellationToken);
            await PersistAsync(request, now, cancellationToken);
            db.EleventaSyncBatches.Add(new EleventaSyncBatch
            {
                SourceId = request.SourceId,
                BatchId = request.BatchId,
                ContractVersion = 1,
                BatchKind = request.Kind,
                PayloadSha256 = payloadHash,
                RecordCount = recordCount,
                AcceptedAt = now
            });
            source.LastConfirmedAt = now;
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return new(request.BatchId, false, recordCount, new DateTimeOffset(now, TimeSpan.Zero), request.CursorAfter);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public async Task<EleventaSyncStatusResponse> GetStatusAsync(Guid sourceId, CancellationToken cancellationToken)
    {
        var latestSnapshot = await db.EleventaInventorySnapshots.AsNoTracking()
            .Where(s => s.SourceId == sourceId && s.IsComplete)
            .OrderByDescending(s => s.ExtractedAt)
            .Select(s => new { s.ReceivedRecordCount, s.ExtractedAt })
            .FirstOrDefaultAsync(cancellationToken);
        var source = await db.EleventaSources.AsNoTracking().SingleAsync(s => s.SourceId == sourceId, cancellationToken);
        var batchCount = await db.EleventaSyncBatches.CountAsync(b => b.SourceId == sourceId, cancellationToken);
        return new(sourceId, source.LastConfirmedAt is null ? null : new DateTimeOffset(DateTime.SpecifyKind(source.LastConfirmedAt.Value, DateTimeKind.Utc)),
            batchCount, latestSnapshot?.ReceivedRecordCount ?? 0,
            latestSnapshot is null ? null : new DateTimeOffset(DateTime.SpecifyKind(latestSnapshot.ExtractedAt, DateTimeKind.Utc)));
    }

    public async Task<EleventaProductPageResponse> GetProductsAsync(Guid sourceId, int skip, int take, CancellationToken ct)
    {
        var query = db.EleventaProducts.AsNoTracking().Where(p => p.SourceId == sourceId);
        var total = await query.CountAsync(ct);
        var products = await query.OrderBy(p => p.SourceProductId).Skip(skip).Take(take)
            .Select(p => new EleventaProductViewDto(p.SourceProductId, p.CodeOriginal, p.DescriptionOriginal,
                p.Cost, p.SalePrice, p.WholesalePrice, p.SourceDepartmentId, p.SourceSupplierId, p.DeletedAt,
                p.TracksInventory, p.CreatedAtOrigin, p.CreatedAtSource, p.CreatedAtConfidence,
                p.FirstSaleAt, p.FirstInventoryMovementAt, p.ObservedAt))
            .ToListAsync(ct);
        return new(skip, take, total, products);
    }

    private async Task PersistAsync(EleventaBatchRequest request, DateTime observedAt, CancellationToken ct)
    {
        switch (request.Kind)
        {
            case "catalog":
                foreach (var item in request.Products)
                {
                    var entity = await db.EleventaProducts.FindAsync([request.SourceId, item.SourceProductId], ct);
                    if (entity is null)
                    {
                        entity = new EleventaProduct { SourceId = request.SourceId, SourceProductId = item.SourceProductId };
                        db.EleventaProducts.Add(entity);
                    }
                    entity.CodeOriginal = item.Code;
                    entity.DescriptionOriginal = item.Description;
                    if (item.Cost is >= 0) entity.Cost = item.Cost;
                    if (item.SalePrice is >= 0) entity.SalePrice = item.SalePrice;
                    if (item.WholesalePrice is >= 0) entity.WholesalePrice = item.WholesalePrice;
                    if (item.DepartmentId is not null) entity.SourceDepartmentId = item.DepartmentId;
                    if (item.SupplierId is not null) entity.SourceSupplierId = item.SupplierId;
                    if (item.CheckedAt is not null) entity.CheckedAt = item.CheckedAt;
                    entity.DeletedAt = item.DeletedAt;
                    entity.IsKit = item.IsKit;
                    entity.TracksInventory = item.TracksInventory;
                    if (item.CreatedAtOrigin is not null)
                    {
                        entity.CreatedAtOrigin = item.CreatedAtOrigin;
                        entity.CreatedAtSource = item.CreatedAtSource;
                        entity.CreatedAtConfidence = item.CreatedAtConfidence;
                    }
                    if (item.FirstSaleAt is not null) entity.FirstSaleAt = item.FirstSaleAt;
                    if (item.FirstInventoryMovementAt is not null) entity.FirstInventoryMovementAt = item.FirstInventoryMovementAt;
                    entity.ObservedAt = observedAt;
                }
                break;
            case "inventory":
                await PersistInventoryAsync(request, observedAt, ct);
                break;
            case "sales":
                await PersistSalesAsync(request, observedAt, ct);
                break;
            case "returns":
                await PersistReturnsAsync(request, observedAt, ct);
                break;
        }
    }

    private async Task PersistInventoryAsync(EleventaBatchRequest request, DateTime observedAt, CancellationToken ct)
    {
        var chunk = request.Inventory!;
        var snapshot = await db.EleventaInventorySnapshots.FindAsync([request.SourceId, chunk.SnapshotId], ct);
        if (snapshot is null)
        {
            snapshot = new EleventaInventorySnapshot
            {
                SourceId = request.SourceId, SnapshotId = chunk.SnapshotId,
                ExtractedAt = chunk.ExtractedAtUtc.UtcDateTime, ExpectedRecordCount = chunk.ExpectedRecordCount
            };
            db.EleventaInventorySnapshots.Add(snapshot);
        }
        else if (snapshot.ExpectedRecordCount != chunk.ExpectedRecordCount ||
                 snapshot.ExtractedAt != chunk.ExtractedAtUtc.UtcDateTime)
        {
            throw new EleventaBatchConflictException("Los fragmentos del snapshot no coinciden en fecha o cantidad esperada.");
        }

        foreach (var row in chunk.Rows)
        {
            var entity = await db.EleventaInventoryRows.FindAsync(
                [request.SourceId, chunk.SnapshotId, row.SourceProductId, row.SourceWarehouseId], ct);
            if (entity is null)
            {
                db.EleventaInventoryRows.Add(new EleventaInventoryRow
                {
                    SourceId = request.SourceId, SnapshotId = chunk.SnapshotId,
                    SourceProductId = row.SourceProductId, SourceWarehouseId = row.SourceWarehouseId, Quantity = row.Quantity
                });
            }
            else if (entity.Quantity != row.Quantity)
            {
                throw new EleventaBatchConflictException("El mismo producto y almacén llegó con cantidades distintas en un snapshot.");
            }
        }

        await db.SaveChangesAsync(ct);
        snapshot.ReceivedRecordCount = await db.EleventaInventoryRows.CountAsync(
            r => r.SourceId == request.SourceId && r.SnapshotId == chunk.SnapshotId, ct);
        if (chunk.IsFinalChunk)
        {
            if (snapshot.ReceivedRecordCount != snapshot.ExpectedRecordCount)
                throw new EleventaBatchConflictException("El snapshot no está completo: el total recibido no coincide con el esperado.");
            snapshot.IsComplete = true;
            snapshot.ValidationStatus = "valid";
            snapshot.CompletedAt = observedAt;
        }
    }

    private async Task PersistSalesAsync(EleventaBatchRequest request, DateTime observedAt, CancellationToken ct)
    {
        foreach (var sale in request.Sales)
        {
            var entity = await db.EleventaSales.FindAsync([request.SourceId, sale.SourceTicketId], ct);
            if (entity is null)
            {
                entity = new EleventaSale { SourceId = request.SourceId, SourceTicketId = sale.SourceTicketId };
                db.EleventaSales.Add(entity);
            }
            entity.Folio = sale.Folio; entity.CreatedAtOrigin = sale.CreatedAtOrigin; entity.SoldAt = sale.SoldAt;
            entity.PaidAt = sale.PaidAt; entity.Total = sale.Total; entity.Profit = sale.Profit;
            entity.IsOpen = sale.IsOpen; entity.IsCancelled = sale.IsCancelled; entity.Currency = sale.Currency;
            entity.ObservedAt = observedAt;
        }
        await db.SaveChangesAsync(ct);
        var ticketIds = request.Sales.Select(s => s.SourceTicketId).ToArray();
        var priorLines = await db.EleventaSaleLines.Where(l => l.SourceId == request.SourceId && ticketIds.Contains(l.SourceTicketId)).ToListAsync(ct);
        foreach (var line in priorLines) line.PresentInLatestSource = false;
        foreach (var sale in request.Sales)
        foreach (var line in sale.Lines)
        {
            var entity = await db.EleventaSaleLines.FindAsync([request.SourceId, line.SourceLineId], ct);
            if (entity is null)
            {
                entity = new EleventaSaleLine { SourceId = request.SourceId, SourceLineId = line.SourceLineId };
                db.EleventaSaleLines.Add(entity);
            }
            entity.SourceTicketId = sale.SourceTicketId; entity.SourceProductId = line.SourceProductId;
            entity.ProductCodeOriginal = line.ProductCode; entity.ProductNameOriginal = line.ProductName;
            entity.Quantity = line.Quantity; entity.ReturnedQuantity = line.ReturnedQuantity;
            entity.UsedPrice = line.UsedPrice; entity.FinalPrice = line.FinalPrice; entity.LineTotal = line.LineTotal;
            entity.AddedAt = line.AddedAt; entity.IsReturned = line.IsReturned; entity.PresentInLatestSource = true; entity.ObservedAt = observedAt;
        }
    }

    private async Task PersistReturnsAsync(EleventaBatchRequest request, DateTime observedAt, CancellationToken ct)
    {
        foreach (var item in request.Returns)
        {
            var entity = await db.EleventaReturns.FindAsync([request.SourceId, item.SourceReturnId], ct);
            if (entity is null)
            {
                entity = new EleventaReturn { SourceId = request.SourceId, SourceReturnId = item.SourceReturnId };
                db.EleventaReturns.Add(entity);
            }
            entity.SourceTicketId = item.SourceTicketId; entity.ReturnedAt = item.ReturnedAt; entity.PaidAt = item.PaidAt;
            entity.ReturnType = item.ReturnType; entity.Cashier = item.Cashier; entity.RegisterName = item.RegisterName;
            entity.Amount = item.Amount; entity.DetailCount = item.Lines.Count; entity.DetailComplete = item.Lines.Count > 0; entity.ObservedAt = observedAt;
        }
        await db.SaveChangesAsync(ct);
        foreach (var item in request.Returns)
        foreach (var line in item.Lines)
        {
            var entity = await db.EleventaReturnLines.FindAsync([request.SourceId, line.SourceReturnLineId], ct);
            if (entity is null)
            {
                entity = new EleventaReturnLine { SourceId = request.SourceId, SourceReturnLineId = line.SourceReturnLineId };
                db.EleventaReturnLines.Add(entity);
            }
            entity.SourceReturnId = item.SourceReturnId; entity.SourceTicketId = item.SourceTicketId;
            entity.ProductCodeOriginal = line.ProductCode; entity.ProductDescriptionOriginal = line.ProductDescription;
            entity.ReturnedQuantity = line.ReturnedQuantity; entity.Amount = line.Amount;
        }
    }

    public static void ValidateRequest(EleventaBatchRequest r, Guid authSource)
    {
        if (r is null || r.Products is null || r.Sales is null || r.Returns is null)
            throw new ArgumentException("El lote está incompleto.");
        if (r.ContractVersion != 1 || r.SourceId == Guid.Empty || r.SourceId != authSource || r.BatchId == Guid.Empty)
            throw new ArgumentException("Versión, origen o BatchId inválido.");
        var kindValid = r.Kind switch
        {
            "catalog" => r.Inventory is null && r.Sales.Count == 0 && r.Returns.Count == 0,
            "inventory" => r.Inventory is not null && r.Products.Count == 0 && r.Sales.Count == 0 && r.Returns.Count == 0,
            "sales" => r.Sales.Count > 0 && r.Products.Count == 0 && r.Inventory is null && r.Returns.Count == 0,
            "returns" => r.Returns.Count > 0 && r.Products.Count == 0 && r.Inventory is null && r.Sales.Count == 0,
            _ => false
        };
        if (!kindValid) throw new ArgumentException("El tipo de lote no coincide con sus colecciones.");
        if (r.Kind == "catalog" && (r.Products.Count == 0 || r.Products.Any(p => p is null || p.SourceProductId <= 0 || p.Code is null || p.Code.Length > 50 || p.Description is null || p.Description.Length > 255)))
            throw new ArgumentException("El catálogo contiene productos vacíos o inválidos.");
        if (r.Products.Select(p => p.SourceProductId).Distinct().Count() != r.Products.Count)
            throw new ArgumentException("El lote contiene IDs de producto repetidos.");
        if (r.Kind == "inventory")
        {
            var i = r.Inventory!;
            if (i.Rows is null || i.SnapshotId == Guid.Empty || i.ExpectedRecordCount < 0 || i.Rows.Count == 0 && i.ExpectedRecordCount != 0 ||
                i.Rows.Any(x => x.SourceProductId <= 0 || x.SourceWarehouseId <= 0) ||
                i.Rows.Select(x => (x.SourceProductId, x.SourceWarehouseId)).Distinct().Count() != i.Rows.Count)
                throw new ArgumentException("El fragmento de inventario es inválido o contiene llaves repetidas.");
        }
        if (r.Kind == "sales" && (r.Sales.Any(s => s is null || s.SourceTicketId <= 0 || s.Lines is null || s.Total < 0 || s.Lines.Any(l => l is null || l.SourceLineId <= 0 || l.Quantity < 0 || l.ReturnedQuantity < 0 || l.UsedPrice < 0 || l.FinalPrice < 0 || l.LineTotal < 0 || l.ProductCode is null || l.ProductCode.Length > 20 || l.ProductName is null || l.ProductName.Length > 255)) ||
            r.Sales.Select(s => s.SourceTicketId).Distinct().Count() != r.Sales.Count ||
            r.Sales.SelectMany(s => s.Lines).Select(l => l.SourceLineId).Distinct().Count() != r.Sales.Sum(s => s.Lines.Count)))
            throw new ArgumentException("El lote de ventas contiene IDs o valores inválidos.");
        if (r.Kind == "returns" && (r.Returns.Any(x => x is null || x.SourceReturnId <= 0 || x.Amount < 0 || x.Lines is null || x.Lines.Any(l => l is null || l.SourceReturnLineId <= 0 || l.ProductCode is null || l.ProductCode.Length > 20 || l.ProductDescription is null || l.ProductDescription.Length > 255 || l.ReturnedQuantity < 0 || l.Amount < 0)) ||
            r.Returns.Select(x => x.SourceReturnId).Distinct().Count() != r.Returns.Count ||
            r.Returns.SelectMany(x => x.Lines).Select(l => l.SourceReturnLineId).Distinct().Count() != r.Returns.Sum(x => x.Lines.Count)))
            throw new ArgumentException("El lote de devoluciones contiene IDs o valores inválidos.");
        if (CountRecords(r) > 5000) throw new ArgumentException("El lote supera el límite de 5000 registros.");
    }

    private static int CountRecords(EleventaBatchRequest r) => r.Products.Count + (r.Inventory?.Rows.Count ?? 0) +
        r.Sales.Count + r.Sales.Sum(s => s.Lines.Count) + r.Returns.Count + r.Returns.Sum(x => x.Lines.Count);

    private static bool FixedHashEquals(string stored, string incoming) =>
        CryptographicOperations.FixedTimeEquals(Convert.FromHexString(stored.Trim()), Convert.FromHexString(incoming));
}
