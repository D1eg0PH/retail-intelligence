using System.Data.Common;
using Eleventa.Agent.Configuration;
using RetailIntelligence.Contracts.Eleventa;
using FirebirdSql.Data.FirebirdClient;

namespace Eleventa.Agent.Services;

public sealed class FirebirdExtractor(AgentOptions options, ILogger<FirebirdExtractor> logger)
{
    private FbConnection CreateConnection()
    {
        var f = options.Firebird;
        var builder = new FbConnectionStringBuilder
        {
            DataSource = f.DataSource, Database = f.Database, Port = f.Port, UserID = f.UserId,
            Password = f.Password, Charset = f.Charset, Dialect = f.Dialect, Pooling = true
        };
        return new FbConnection(builder.ConnectionString);
    }

    public async Task<int> ProbeReadOnlyAsync(CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(options.Firebird.Database) || string.IsNullOrWhiteSpace(options.Firebird.UserId) || string.IsNullOrWhiteSpace(options.Firebird.Password))
            throw new InvalidOperationException("Configura la conexión Firebird en variables externas.");
        await using var connection = CreateConnection();
        await connection.OpenAsync(ct);
        var transactionOptions = new FbTransactionOptions
        {
            TransactionBehavior = FbTransactionBehavior.Read | FbTransactionBehavior.ReadCommitted |
                                  FbTransactionBehavior.RecVersion | FbTransactionBehavior.NoWait
        };
        await using var transaction = await connection.BeginTransactionAsync(transactionOptions, ct);
        await using var command = new FbCommand("SELECT COUNT(*) FROM PRODUCTOS", connection, transaction);
        var count = Convert.ToInt32(await command.ExecuteScalarAsync(ct), System.Globalization.CultureInfo.InvariantCulture);
        await transaction.CommitAsync(ct);
        return count;
    }

    public async Task<IReadOnlyList<(EleventaBatchRequest Batch, string? CursorKey, string? CursorAfter)>> ExtractAsync(
        int salesCursor, int returnCursor, DateTime salesWindowStart, DateTime returnsWindowStart,
        bool includeCatalog, bool includeInventory, CancellationToken ct)
    {
        ValidateFirebirdConfiguration();
        if (options.Integration.SourceId == Guid.Empty)
            throw new InvalidOperationException("Configura Integration:SourceId con el identificador registrado por la API.");
        await using var connection = CreateConnection();
        await connection.OpenAsync(ct);
        var transactionOptions = new FbTransactionOptions
        {
            TransactionBehavior = FbTransactionBehavior.Read | FbTransactionBehavior.ReadCommitted |
                                  FbTransactionBehavior.RecVersion | FbTransactionBehavior.NoWait
        };
        await using var transaction = await connection.BeginTransactionAsync(transactionOptions, ct);
        var batches = new List<(EleventaBatchRequest, string?, string?)>();
        var now = DateTimeOffset.UtcNow;
        Dictionary<string, int> productIdsByCode;

        if (includeCatalog)
        {
            var products = await ReadProductsAsync(connection, transaction, ct);
            productIdsByCode = products.ToDictionary(p => p.Code, p => p.SourceProductId, StringComparer.Ordinal);
            var chunks = products.Chunk(2000).ToArray();
            for (var index = 0; index < chunks.Length; index++)
            {
                var batch = CreateBatch("catalog", now, products: chunks[index].ToList());
                if (index == chunks.Length - 1) batch.CursorAfter = now.ToString("O");
                batches.Add((batch,
                    index == chunks.Length - 1 ? "catalog-last-at" : null,
                    index == chunks.Length - 1 ? now.ToString("O") : null));
            }
        }
        else
        {
            productIdsByCode = await ReadProductIdMapAsync(connection, transaction, ct);
        }
        if (includeInventory)
        {
            var rows = await ReadInventoryAsync(connection, transaction, ct);
            var snapshotId = Guid.NewGuid();
            var chunks = rows.Chunk(2000).ToArray();
            if (chunks.Length == 0) chunks = [[]];
            for (var index = 0; index < chunks.Length; index++)
            {
                var batch = CreateBatch("inventory", now, inventory: new EleventaInventoryChunkDto
            {
                SnapshotId = snapshotId, ExtractedAtUtc = now,
                ExpectedRecordCount = rows.Count, IsFinalChunk = index == chunks.Length - 1, Rows = chunks[index].ToList()
            });
                if (index == chunks.Length - 1) batch.CursorAfter = now.ToString("O");
                batches.Add((batch, index == chunks.Length - 1 ? "inventory-last-at" : null,
                index == chunks.Length - 1 ? now.ToString("O") : null));
            }
        }

        var sales = await ReadSalesAsync(connection, transaction, salesCursor, salesWindowStart, productIdsByCode, ct);
        if (sales.Count > 0)
        {
            var after = sales.Max(x => x.SourceTicketId);
            var batch = CreateBatch("sales", now, sales: sales); batch.CursorAfter = after.ToString();
            batches.Add((batch, "sales-scan-cursor", after.ToString()));
        }
        // Keep the last confirmed cursor when there are no new tickets. Resetting it here
        // would make the agent replay the entire lookback window on every idle cycle.

        var returns = await ReadReturnsAsync(connection, transaction, returnCursor, returnsWindowStart, ct);
        if (returns.Count > 0)
        {
            var after = returns.Max(x => x.SourceReturnId);
            var batch = CreateBatch("returns", now, returns: returns); batch.CursorAfter = after.ToString();
            batches.Add((batch, "returns-scan-cursor", after.ToString()));
        }
        // Keep the last confirmed cursor when there are no new returns so subsequent
        // cycles remain incremental instead of replaying the lookback window.

        await transaction.CommitAsync(ct);
        logger.LogInformation("Extracción Firebird terminada: {BatchCount} lotes pendientes.", batches.Count);
        return batches;
    }

    private static async Task<Dictionary<string, int>> ReadProductIdMapAsync(FbConnection connection, DbTransaction transaction, CancellationToken ct)
    {
        var map = new Dictionary<string, int>(StringComparer.Ordinal);
        await using var command = new FbCommand("SELECT ID,CODIGO FROM PRODUCTOS", connection, (FbTransaction)transaction);
        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct)) map[GetString(reader, "CODIGO")] = GetInt(reader, "ID");
        return map;
    }

    private async Task<List<EleventaProductDto>> ReadProductsAsync(FbConnection connection, DbTransaction transaction, CancellationToken ct)
    {
        var products = new List<EleventaProductDto>();
        await using var command = new FbCommand("""
            SELECT p.ID, p.CODIGO, p.DESCRIPCION, p.PCOSTO, p.PVENTA, p.MAYOREO, p.DEPT, p.PROVID,
                   d.NOMBRE AS DEPARTMENT_NAME, pr.NOMBRE AS SUPPLIER_NAME,
                   p.CHECADO_EN, p.ELIMINADO_EN, p.ES_KIT, p.USA_INVENTARIO
            FROM PRODUCTOS p
            LEFT JOIN DEPARTAMENTOS d ON d.ID = p.DEPT
            LEFT JOIN PROVEEDORES pr ON pr.ID = p.PROVID
            ORDER BY p.ID
            """, connection, (FbTransaction)transaction);
        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            products.Add(new EleventaProductDto
            {
                SourceProductId = GetInt(reader, "ID"), Code = GetString(reader, "CODIGO"),
                Description = GetString(reader, "DESCRIPCION"), Cost = GetDecimal(reader, "PCOSTO"),
                SalePrice = GetDecimal(reader, "PVENTA"), WholesalePrice = GetDecimal(reader, "MAYOREO"),
                DepartmentId = GetNullableInt(reader, "DEPT"), DepartmentName = NullIfBlank(GetString(reader, "DEPARTMENT_NAME")),
                SupplierId = GetNullableInt(reader, "PROVID"), SupplierName = NullIfBlank(GetString(reader, "SUPPLIER_NAME")),
                CheckedAt = GetDate(reader, "CHECADO_EN"), DeletedAt = GetDate(reader, "ELIMINADO_EN"),
                IsKit = IsTrue(GetString(reader, "ES_KIT")), TracksInventory = IsTrue(GetString(reader, "USA_INVENTARIO")),
                // No existe una fecha universal de creación en PRODUCTOS. La primera venta/movimiento
                // se mantiene como evidencia observada, nunca como fecha de alta.
                CreatedAtOrigin = null, CreatedAtSource = null, CreatedAtConfidence = null
            });
        }

        var firstSaleByCode = new Dictionary<string, DateTime>(StringComparer.Ordinal);
        await using (var firstSales = new FbCommand("""
            SELECT A.PRODUCTO_CODIGO, MIN(T.VENDIDO_EN) AS PRIMERA_VENTA
            FROM VENTATICKETS_ARTICULOS A JOIN VENTATICKETS T ON T.ID=A.TICKET_ID
            WHERE T.ESTA_ABIERTO='f' AND T.ESTA_CANCELADO='f'
            GROUP BY A.PRODUCTO_CODIGO
            """, connection, (FbTransaction)transaction))
        await using (var salesReader = await firstSales.ExecuteReaderAsync(ct))
        {
            while (await salesReader.ReadAsync(ct))
                if (!salesReader.IsDBNull(1)) firstSaleByCode[GetString(salesReader, 0)] = salesReader.GetDateTime(1);
        }
        foreach (var product in products)
            if (firstSaleByCode.TryGetValue(product.Code, out var firstSale)) product.FirstSaleAt = firstSale;

        var firstMovementById = new Dictionary<int, DateTime>();
        await using (var firstMovements = new FbCommand("""
            SELECT PRODUCTO_ID, MIN(CUANDO_FUE) AS PRIMER_MOVIMIENTO
            FROM INVENTARIO_HISTORIAL GROUP BY PRODUCTO_ID
            """, connection, (FbTransaction)transaction))
        await using (var movementReader = await firstMovements.ExecuteReaderAsync(ct))
        {
            while (await movementReader.ReadAsync(ct))
                if (!movementReader.IsDBNull(1)) firstMovementById[GetInt(movementReader, 0)] = movementReader.GetDateTime(1);
        }
        foreach (var product in products)
            if (firstMovementById.TryGetValue(product.SourceProductId, out var firstMovement)) product.FirstInventoryMovementAt = firstMovement;
        return products;
    }

    private static async Task<List<EleventaInventoryRowDto>> ReadInventoryAsync(FbConnection connection, DbTransaction transaction, CancellationToken ct)
    {
        var rows = new List<EleventaInventoryRowDto>();
        await using var command = new FbCommand("SELECT B.PRODUCTO_ID, B.ALMACEN_ID, B.CANTIDAD_ACTUAL, P.PCOSTO FROM INVENTARIO_BALANCES B LEFT JOIN PRODUCTOS P ON P.ID=B.PRODUCTO_ID ORDER BY B.ALMACEN_ID, B.PRODUCTO_ID", connection, (FbTransaction)transaction);
        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
            rows.Add(new EleventaInventoryRowDto { SourceProductId = GetInt(reader, "PRODUCTO_ID"), SourceWarehouseId = GetInt(reader, "ALMACEN_ID"), Quantity = GetDecimal(reader, "CANTIDAD_ACTUAL") ?? 0m, UnitCost = GetDecimal(reader, "PCOSTO") });
        return rows;
    }

    private async Task<List<EleventaSaleDto>> ReadSalesAsync(FbConnection connection, DbTransaction transaction, int cursor,
        DateTime since, IReadOnlyDictionary<string, int> productIdsByCode, CancellationToken ct, int? requestedLimit = null)
    {
        var limit = requestedLimit ?? Math.Clamp(options.Integration.BatchSize, 1, 500);
        var tickets = new List<EleventaSaleDto>();
        await using (var command = new FbCommand($"""
            SELECT FIRST {limit} ID,FOLIO,CREADO_EN,VENDIDO_EN,PAGADO_EN,TOTAL,GANANCIA,
                   ESTA_ABIERTO,ESTA_CANCELADO,MONEDA
            FROM VENTATICKETS WHERE ID > @cursor AND VENDIDO_EN >= @since ORDER BY ID
            """, connection, (FbTransaction)transaction))
        {
            AddParameter(command, "@cursor", cursor); AddParameter(command, "@since", since);
            await using var reader = await command.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
                tickets.Add(new EleventaSaleDto
                {
                    SourceTicketId = GetInt(reader, "ID"), Folio = GetNullableInt(reader, "FOLIO"),
                    CreatedAtOrigin = GetDate(reader, "CREADO_EN"), SoldAt = GetDate(reader, "VENDIDO_EN"),
                    PaidAt = GetDate(reader, "PAGADO_EN"), Total = GetDecimal(reader, "TOTAL"), Profit = GetDecimal(reader, "GANANCIA"),
                    IsOpen = IsTrue(GetString(reader, "ESTA_ABIERTO")), IsCancelled = IsTrue(GetString(reader, "ESTA_CANCELADO")),
                    Currency = NullIfBlank(GetString(reader, "MONEDA"))
                });
        }
        if (tickets.Count == 0) return tickets;

        var lines = new Dictionary<int, EleventaSaleDto>();
        foreach (var ticket in tickets) lines[ticket.SourceTicketId] = ticket;
        await using (var command = new FbCommand(BuildInQuery("""
            SELECT ID,TICKET_ID,PRODUCTO_CODIGO,PRODUCTO_NOMBRE,CANTIDAD,CANTIDAD_DEVUELTA,
                   PRECIO_USADO,PRECIO_FINAL,TOTAL_ARTICULO,AGREGADO_EN,FUE_DEVUELTO
            FROM VENTATICKETS_ARTICULOS WHERE TICKET_ID IN ({0}) ORDER BY TICKET_ID,ID
            """, tickets.Count), connection, (FbTransaction)transaction))
        {
            AddIdParameters(command, tickets.Select(t => t.SourceTicketId));
            await using var reader = await command.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                var ticketId = GetInt(reader, "TICKET_ID");
                if (!lines.TryGetValue(ticketId, out var ticket)) continue;
                var productCode = GetString(reader, "PRODUCTO_CODIGO");
                ticket.Lines.Add(new EleventaSaleLineDto
                {
                    SourceLineId = GetInt(reader, "ID"), SourceProductId = productIdsByCode.TryGetValue(productCode, out var sourceProductId) ? sourceProductId : null,
                    ProductCode = productCode,
                    ProductName = GetString(reader, "PRODUCTO_NOMBRE"), Quantity = GetDecimal(reader, "CANTIDAD") ?? 0m,
                    ReturnedQuantity = GetDecimal(reader, "CANTIDAD_DEVUELTA") ?? 0m,
                    UsedPrice = GetDecimal(reader, "PRECIO_USADO"), FinalPrice = GetDecimal(reader, "PRECIO_FINAL"),
                    LineTotal = GetDecimal(reader, "TOTAL_ARTICULO"), AddedAt = GetDate(reader, "AGREGADO_EN"),
                    IsReturned = IsTrue(GetString(reader, "FUE_DEVUELTO"))
                });
            }
        }
        var rowCount = tickets.Sum(ticket => 1 + ticket.Lines.Count);
        if (rowCount > 5000 && limit > 1)
            return await ReadSalesAsync(connection, transaction, cursor, since, productIdsByCode, ct, Math.Max(1, limit / 2));
        if (rowCount > 5000) throw new InvalidDataException("Un solo ticket supera el máximo de filas permitido por lote.");
        return tickets;
    }

    private async Task<List<EleventaReturnDto>> ReadReturnsAsync(FbConnection connection, DbTransaction transaction, int cursor,
        DateTime since, CancellationToken ct, int? requestedLimit = null)
    {
        var limit = requestedLimit ?? Math.Clamp(options.Integration.BatchSize, 1, 500);
        var returns = new List<EleventaReturnDto>();
        await using (var command = new FbCommand($"""
            SELECT FIRST {limit} ID,TICKET_ID,TIPO_DEVOLUCION,PAGADO_EN,DEVUELTO_EN,CAJERO,CAJA,TOTAL_DEVUELTO
            FROM DEVOLUCIONES WHERE ID > @cursor AND DEVUELTO_EN >= @since ORDER BY ID
            """, connection, (FbTransaction)transaction))
        {
            AddParameter(command, "@cursor", cursor); AddParameter(command, "@since", since);
            await using var reader = await command.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
                returns.Add(new EleventaReturnDto
                {
                    SourceReturnId = GetInt(reader, "ID"), SourceTicketId = GetNullableInt(reader, "TICKET_ID"),
                    ReturnType = NullIfBlank(GetString(reader, "TIPO_DEVOLUCION")), PaidAt = GetDate(reader, "PAGADO_EN"),
                    ReturnedAt = GetDate(reader, "DEVUELTO_EN"), Cashier = NullIfBlank(GetString(reader, "CAJERO")),
                    RegisterName = NullIfBlank(GetString(reader, "CAJA")), Amount = GetDecimal(reader, "TOTAL_DEVUELTO") ?? 0m
                });
        }
        if (returns.Count == 0) return returns;
        await using (var command = new FbCommand(BuildInQuery("""
            SELECT ID,DEVOLUCION_ID,TICKET_ID,CODIGO_PRODUCTO,DESCRIPCION_PRODUCTO,CANTIDAD_DEVUELTA,DINERO_DEVUELTO
            FROM DEVOLUCIONES_ARTICULOS WHERE DEVOLUCION_ID IN ({0}) ORDER BY DEVOLUCION_ID,ID
            """, returns.Count), connection, (FbTransaction)transaction))
        {
            AddIdParameters(command, returns.Select(x => x.SourceReturnId));
            var byId = returns.ToDictionary(x => x.SourceReturnId);
            await using var reader = await command.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                var returnId = GetInt(reader, "DEVOLUCION_ID");
                if (!byId.TryGetValue(returnId, out var item)) continue;
                item.Lines.Add(new EleventaReturnLineDto
                {
                    SourceReturnLineId = GetInt(reader, "ID"), ProductCode = GetString(reader, "CODIGO_PRODUCTO"),
                    ProductDescription = GetString(reader, "DESCRIPCION_PRODUCTO"),
                    ReturnedQuantity = GetDecimal(reader, "CANTIDAD_DEVUELTA") ?? 0m,
                    Amount = GetDecimal(reader, "DINERO_DEVUELTO") ?? 0m
                });
            }
        }
        var rowCount = returns.Sum(item => 1 + item.Lines.Count);
        if (rowCount > 5000 && limit > 1)
            return await ReadReturnsAsync(connection, transaction, cursor, since, ct, Math.Max(1, limit / 2));
        if (rowCount > 5000) throw new InvalidDataException("Una sola devolución supera el máximo de filas permitido por lote.");
        return returns;
    }

    private EleventaBatchRequest CreateBatch(string kind, DateTimeOffset now, List<EleventaProductDto>? products = null,
        EleventaInventoryChunkDto? inventory = null, List<EleventaSaleDto>? sales = null, List<EleventaReturnDto>? returns = null) => new()
    {
        ContractVersion = EleventaContract.CurrentVersion, SourceId = options.Integration.SourceId, BatchId = Guid.NewGuid(),
        Kind = kind, CreatedAtUtc = now, Products = products ?? [], Inventory = inventory, Sales = sales ?? [], Returns = returns ?? []
    };

    private void ValidateFirebirdConfiguration()
    {
        var f = options.Firebird;
        if (string.IsNullOrWhiteSpace(f.Database) || string.IsNullOrWhiteSpace(f.UserId) || string.IsNullOrWhiteSpace(f.Password))
            throw new InvalidOperationException("Configura Firebird:Database, Firebird:UserId y Firebird:Password mediante variables externas.");
    }

    private static string BuildInQuery(string template, int count) => string.Format(System.Globalization.CultureInfo.InvariantCulture,
        template, string.Join(",", Enumerable.Range(0, count).Select(i => "@id" + i)));
    private static void AddIdParameters(FbCommand command, IEnumerable<int> ids)
    { var index = 0; foreach (var id in ids) AddParameter(command, "@id" + index++, id); }
    private static void AddParameter(FbCommand command, string name, object value)
    { var parameter = command.CreateParameter(); parameter.ParameterName = name; parameter.Value = value; command.Parameters.Add(parameter); }
    private static bool IsTrue(string value) => value.Equals("t", StringComparison.OrdinalIgnoreCase) || value.Equals("true", StringComparison.OrdinalIgnoreCase) || value == "1";
    private static string NullIfBlank(string value) => string.IsNullOrWhiteSpace(value) ? null! : value.Trim();
    private static string GetString(DbDataReader r, string name) => r[name] is DBNull ? string.Empty : Convert.ToString(r[name], System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty;
    private static string GetString(DbDataReader r, int ordinal) => r.IsDBNull(ordinal) ? string.Empty : Convert.ToString(r.GetValue(ordinal), System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty;
    private static int GetInt(DbDataReader r, string name) => Convert.ToInt32(r[name], System.Globalization.CultureInfo.InvariantCulture);
    private static int GetInt(DbDataReader r, int ordinal) => Convert.ToInt32(r.GetValue(ordinal), System.Globalization.CultureInfo.InvariantCulture);
    private static int? GetNullableInt(DbDataReader r, string name) => r[name] is DBNull ? null : GetInt(r, name);
    private static decimal? GetDecimal(DbDataReader r, string name) => r[name] is DBNull ? null : Convert.ToDecimal(r[name], System.Globalization.CultureInfo.InvariantCulture);
    private static DateTime? GetDate(DbDataReader r, string name) => r[name] is DBNull ? null : Convert.ToDateTime(r[name], System.Globalization.CultureInfo.InvariantCulture);
}
