using Microsoft.EntityFrameworkCore;
using Npgsql;
using RetailIntelligence.Api.Data;
using RetailIntelligence.Api.Entities;
using RetailIntelligence.Api.Services;

internal static class Program
{
    private static async Task Main()
    {
        var connectionString = Environment.GetEnvironmentVariable(
            "RETAIL_INTELLIGENCE_TEST_DB");
        var testConnection = string.IsNullOrWhiteSpace(connectionString)
            ? null
            : new NpgsqlConnectionStringBuilder(connectionString);

        if (testConnection is null ||
            testConnection.Host != "127.0.0.1" ||
            testConnection.Port != 55432 ||
            testConnection.Database != "retail_intelligence_test" ||
            testConnection.Username != "retail_test")
        {
            throw new InvalidOperationException(
                "Set RETAIL_INTELLIGENCE_TEST_DB to the isolated local test database. " +
                "The test runner only accepts Host=127.0.0.1, Port=55432, " +
                "Database=retail_intelligence_test, and Username=retail_test.");
        }

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(testConnection.ConnectionString)
            .Options;

        await using var context = new AppDbContext(options);
        var startupDeadline = DateTime.UtcNow.AddSeconds(30);
        while (!await context.Database.CanConnectAsync())
        {
            if (DateTime.UtcNow >= startupDeadline)
            {
                throw new TimeoutException("The isolated PostgreSQL test database did not become available.");
            }

            await Task.Delay(500);
        }

        await context.Database.EnsureCreatedAsync();
        if (await context.Productos.AnyAsync() ||
            await context.Importaciones.AnyAsync() ||
            await context.Pedidos.AnyAsync())
        {
            throw new InvalidOperationException(
                "The isolated test database must be empty before the suite runs.");
        }
        await SeedBaseDataAsync(context);

        await VerifyPeriodAsync(context, 14);
        await VerifyPeriodAsync(context, 28);
        await VerifyPeriodAsync(context, 30);
        await VerifyInventoryProviderGroupingAndLatestImportAsync(context);
        await VerifyInvalidPeriodPreservesExistingOrdersAsync(context);

        Console.WriteLine("All isolated PedidoService integration checks passed.");
    }

    static async Task SeedBaseDataAsync(AppDbContext context)
    {
        var now = DateTime.UtcNow;

        context.EstadosPedido.Add(new EstadoPedido
        {
            IdEstado = 1,
            Nombre = "Pendiente",
            CreatedAt = now,
            UpdatedAt = now
        });

        context.Departamentos.Add(new Departamento
        {
            IdDepartamento = 1,
            Nombre = "Pruebas",
            Activo = true,
            CreatedAt = now,
            UpdatedAt = now
        });

        context.Proveedores.AddRange(
            new Proveedor
            {
                IdProveedor = 1,
                Nombre = "Proveedor A",
                Activo = true,
                CreatedAt = now,
                UpdatedAt = now
            },
            new Proveedor
            {
                IdProveedor = 2,
                Nombre = "Proveedor B",
                Activo = true,
                CreatedAt = now,
                UpdatedAt = now
            },
            new Proveedor
            {
                IdProveedor = 3,
                Nombre = "Proveedor excluido",
                Activo = true,
                CreatedAt = now,
                UpdatedAt = now
            });

        context.Productos.AddRange(
            CrearProducto(1, 1, "Producto principal", now),
            CrearProducto(2, 1, "Producto proveedor A", now),
            CrearProducto(3, 2, "Producto proveedor B", now),
            CrearProducto(4, 1, "Producto con inventario suficiente", now),
            CrearProducto(5, 3, "Producto proveedor excluido", now));

        context.ConfiguracionSistema.Add(new ConfiguracionSistema
        {
            IdConfiguracion = 1,
            SemanasPromedio = 1,
            DiasInventarioObjetivo = 5,
            DiasProductoMuerto = 90,
            PorcentajeColchon = 10m,
            CreatedAt = now,
            UpdatedAt = now
        });

        var importacionInventario = new Importacion
        {
            TipoArchivo = "INVENTARIO",
            NombreArchivo = "inventario-prueba.xlsx",
            HashArchivo = Guid.NewGuid().ToString(),
            RegistrosImportados = 2,
            CreatedAt = now,
            UpdatedAt = now
        };
        context.Importaciones.Add(importacionInventario);
        await context.SaveChangesAsync();

        context.Inventarios.AddRange(
            CrearInventario(1, importacionInventario.IdImportacion, 0m, now),
            CrearInventario(4, importacionInventario.IdImportacion, 15m, now),
            CrearInventario(4, importacionInventario.IdImportacion, 25m, now),
            CrearInventario(5, importacionInventario.IdImportacion, 0m, now));
        await context.SaveChangesAsync();
    }

    static Producto CrearProducto(
        int id,
        int idProveedor,
        string nombre,
        DateTime now) => new()
    {
        IdProducto = id,
        IdProveedor = idProveedor,
        IdDepartamento = 1,
        Codigo = id.ToString(),
        Nombre = nombre,
        Activo = true,
        EsEsencial = false,
        StockMinimo = 0m,
        StockMaximo = 0m,
        UnidadMedida = "PZA",
        CostoActual = 2m,
        PrecioVentaActual = 3m,
        Descontinuado = false,
        CreatedAt = now,
        UpdatedAt = now
    };

    static Inventario CrearInventario(
        int idProducto,
        int idImportacion,
        decimal existencia,
        DateTime now) => new()
    {
        IdProducto = idProducto,
        IdImportacion = idImportacion,
        Existencia = existencia,
        PrecioCosto = 2m,
        CreatedAt = now,
        UpdatedAt = now
    };

    static async Task VerifyPeriodAsync(AppDbContext context, int days)
    {
        await ClearSalesAsync(context);

        var now = DateTime.UtcNow;
        var fechaFin = new DateOnly(2026, 5, days);
        var importacion = await AddSalesImportAsync(
            context,
            fechaFin.AddDays(-(days - 1)),
            fechaFin,
            [(1, (decimal)days)],
            now);

        await new PedidoService(context).GenerarPedidosAsync();

        var detalles = await context.PedidoDetalles.AsNoTracking().ToListAsync();
        AssertEqual(1, detalles.Count, $"{days}-day detail count");
        AssertEqual(1, detalles[0].IdProducto, $"{days}-day product");
        AssertEqual(31m, detalles[0].CantidadSugerida, $"{days}-day suggestion");

        var selectedImport = await context.Importaciones
            .Where(i => i.IdImportacion == importacion.IdImportacion)
            .SingleAsync();
        AssertEqual(days, selectedImport.FechaFinPeriodo.DayNumber -
            selectedImport.FechaInicioPeriodo.DayNumber + 1,
            $"{days}-day inclusive period length");

        Console.WriteLine($"PASS: {days}-day history yields a 28-day target and 31 units with 10% buffer.");
        context.ChangeTracker.Clear();
    }

    static async Task VerifyInventoryProviderGroupingAndLatestImportAsync(
        AppDbContext context)
    {
        await ClearSalesAsync(context);

        var now = DateTime.UtcNow;
        await AddSalesImportAsync(
            context,
            new DateOnly(2025, 12, 1),
            new DateOnly(2025, 12, 14),
            [(1, 140m)],
            now.AddMinutes(-1));

        var latestImport = await AddSalesImportAsync(
            context,
            new DateOnly(2026, 1, 1),
            new DateOnly(2026, 1, 30),
            [(1, 30m), (2, 60m), (3, 30m), (4, 30m), (5, 30m)],
            now);

        await new PedidoService(context).GenerarPedidosAsync();

        var pedidos = await context.Pedidos
            .AsNoTracking()
            .Include(p => p.PedidoDetalles)
            .OrderBy(p => p.IdProveedor)
            .ToListAsync();

        AssertEqual(2, pedidos.Count, "orders grouped by supplier");
        AssertEqual(1, pedidos[0].IdProveedor, "first supplier group");
        AssertEqual(2, pedidos[0].PedidoDetalles.Count, "supplier 1 details");
        AssertEqual(2, pedidos[1].IdProveedor, "second supplier group");
        AssertEqual(1, pedidos[1].PedidoDetalles.Count, "supplier 2 details");

        var suggestions = pedidos
            .SelectMany(p => p.PedidoDetalles)
            .ToDictionary(d => d.IdProducto, d => d.CantidadSugerida);
        AssertEqual(31m, suggestions[1], "latest import selected over older import");
        AssertEqual(62m, suggestions[2], "decimal average and final ceiling");
        AssertEqual(31m, suggestions[3], "second supplier suggestion");
        AssertEqual(false, suggestions.ContainsKey(4), "stock above target excluded");
        AssertEqual(false, suggestions.ContainsKey(5), "excluded supplier omitted");

        var selectedSales = await context.VentasPeriodo
            .Where(v => v.IdImportacion == latestImport.IdImportacion)
            .SumAsync(v => v.CantidadVendida);
        AssertEqual(180m, selectedSales, "latest import fixture was saved");

        Console.WriteLine("PASS: latest import, inventory deduction, supplier grouping, and existing supplier exclusion.");
        context.ChangeTracker.Clear();
    }

    static async Task VerifyInvalidPeriodPreservesExistingOrdersAsync(
        AppDbContext context)
    {
        await ClearSalesAsync(context);
        await context.PedidoDetalles.ExecuteDeleteAsync();
        await context.Pedidos.ExecuteDeleteAsync();

        var now = DateTime.UtcNow;
        var invalidImport = await AddSalesImportAsync(
            context,
            new DateOnly(2026, 6, 2),
            new DateOnly(2026, 6, 1),
            [],
            now);

        var existingOrder = new Pedido
        {
            IdProveedor = 1,
            IdEstado = 1,
            FechaPedido = DateOnly.FromDateTime(DateTime.Today),
            CreatedAt = now,
            UpdatedAt = now,
            PedidoDetalles =
            {
                new PedidoDetalle
                {
                    IdProducto = 1,
                    CantidadSugerida = 7m,
                    CostoUnitario = 2m,
                    CreatedAt = now,
                    UpdatedAt = now
                }
            }
        };
        context.Pedidos.Add(existingOrder);
        await context.SaveChangesAsync();

        var threw = false;
        try
        {
            await new PedidoService(context).GenerarPedidosAsync();
        }
        catch (Exception exception) when (
            exception.Message.Contains("período", StringComparison.OrdinalIgnoreCase))
        {
            threw = true;
        }

        AssertEqual(true, threw, "invalid period rejected");
        AssertEqual(1, await context.Pedidos.CountAsync(), "invalid period does not truncate existing orders");
        AssertEqual(7m, await context.PedidoDetalles
            .Select(d => d.CantidadSugerida)
            .SingleAsync(), "existing order detail preserved");

        Console.WriteLine("PASS: invalid date range rejected before existing orders are cleared.");
    }

    static async Task ClearSalesAsync(AppDbContext context)
    {
        await context.VentasPeriodo.ExecuteDeleteAsync();
        await context.Importaciones
            .Where(i => i.TipoArchivo == "VENTAS_PERIODO")
            .ExecuteDeleteAsync();
    }

    static async Task<Importacion> AddSalesImportAsync(
        AppDbContext context,
        DateOnly start,
        DateOnly end,
        (int ProductId, decimal Quantity)[] sales,
        DateTime createdAt)
    {
        var importacion = new Importacion
        {
            TipoArchivo = "VENTAS_PERIODO",
            NombreArchivo = "ventas-prueba.xlsx",
            FechaInicioPeriodo = start,
            FechaFinPeriodo = end,
            HashArchivo = Guid.NewGuid().ToString(),
            RegistrosImportados = sales.Length,
            CreatedAt = createdAt,
            UpdatedAt = createdAt
        };
        context.Importaciones.Add(importacion);
        await context.SaveChangesAsync();

        foreach (var (productId, quantity) in sales)
        {
            context.VentasPeriodo.Add(new VentaPeriodo
            {
                IdProducto = productId,
                IdImportacion = importacion.IdImportacion,
                CantidadVendida = quantity,
                PrecioUsado = 3m,
                PrecioCosto = 2m,
                Importe = quantity * 3m,
                CreatedAt = createdAt,
                UpdatedAt = createdAt
            });
        }

        await context.SaveChangesAsync();
        return importacion;
    }

    static void AssertEqual<T>(T expected, T actual, string scenario)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
        {
            throw new InvalidOperationException(
                $"Assertion failed ({scenario}): expected '{expected}', got '{actual}'.");
        }
    }
}
