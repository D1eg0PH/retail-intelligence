using Microsoft.EntityFrameworkCore;
using RetailIntelligence.Api.Data;
using RetailIntelligence.Api.Entities;
using RetailIntelligence.Api.Interfaces;
namespace RetailIntelligence.Api.Services;

public class PedidoService : IPedidoService
{
    private readonly AppDbContext _context;

    public PedidoService(AppDbContext context)
    {
        _context = context;
    }


    // METODO PARA GENERAR PEDIDOS
    public async Task GenerarPedidosAsync()
    {

        await _context.Database.ExecuteSqlRawAsync( "TRUNCATE TABLE pedidodetalle RESTART IDENTITY CASCADE");

        await _context.Database.ExecuteSqlRawAsync( "TRUNCATE TABLE pedidos RESTART IDENTITY CASCADE");

        var importacionInventario = await _context.Importaciones
            .FirstOrDefaultAsync(i => i.TipoArchivo == "INVENTARIO");


        if (importacionInventario == null)
        {
            throw new Exception("No existe ninguna importación de inventario.");
        }

        Console.WriteLine($"Inventario utilizado: {importacionInventario.IdImportacion}");
        Console.WriteLine(
            $"Inventario utilizado: {importacionInventario.IdImportacion}");


        var importacionVentas = await _context.Importaciones
            .FirstOrDefaultAsync(i => i.TipoArchivo == "VENTAS_PERIODO");

        if (importacionVentas == null)
        {
            throw new Exception("No existe ninguna importación de ventas.");
        }



        var diasPeriodo = Math.Max(1,importacionVentas.FechaFinPeriodo.DayNumber - importacionVentas.FechaInicioPeriodo.DayNumber + 1);


        var semanasPeriodo = Math.Max(1m,diasPeriodo / 7m);


        Console.WriteLine( $"Semanas analizadas: {semanasPeriodo}");


        var ventasPorProducto = await _context.VentasPeriodo
            .Where(v =>
                v.IdImportacion ==
                importacionVentas.IdImportacion)
            .GroupBy(v => v.IdProducto)
            .Select(g => new
            {
                IdProducto = g.Key,
                CantidadVendida = g.Sum(x => x.CantidadVendida)
            })
            .ToListAsync();


        Console.WriteLine($"Productos con ventas: {ventasPorProducto.Count}");

        var inventarioActual = await _context.Inventarios.Where(i => i.IdImportacion == importacionInventario.IdImportacion).GroupBy(i => i.IdProducto).ToDictionaryAsync(g => g.Key,g => g.Sum(x => x.Existencia));

        var pedidosPorProveedor = new Dictionary<int, Pedido>();

        var productos = await _context.Productos.ToDictionaryAsync( p => p.IdProducto, p => p);

        int productosConPedido = 0;

        foreach (var venta in ventasPorProducto)
        {   
            var promedioSemanal = venta.CantidadVendida / semanasPeriodo;

            var stockObjetivo = promedioSemanal * 1.20m;

            var existenciaActual = inventarioActual.GetValueOrDefault(venta.IdProducto,0);

            var pedidoSugerido = Math.Ceiling(stockObjetivo - existenciaActual);

            pedidoSugerido = Math.Max(0, pedidoSugerido);

            if (!productos.TryGetValue(venta.IdProducto,out var producto))
            {
                continue;
            }

            Console.WriteLine(
                $"{producto.Nombre,-40}" +
                $" Vendido:{venta.CantidadVendida,-5}" +
                $" Exist:{existenciaActual,-5}" +
                $" Objetivo:{stockObjetivo,-8:F2}" +
                $" Pedido:{pedidoSugerido}");

            if (pedidoSugerido <= 0)
            {
                continue;
            }


            // Ignorar productos sin proveedor real
            if (producto.IdProveedor == 3)
            {
                continue;
            }

            productosConPedido++;

            Console.ForegroundColor = ConsoleColor.Green;

            Console.WriteLine(
            $"PEDIDO -> {producto.Nombre} | " +
            $"Proveedor:{producto.IdProveedor} | " +
            $"Pedido:{pedidoSugerido}");

            Console.ResetColor();

            if (!pedidosPorProveedor.ContainsKey(producto.IdProveedor))
            {
                var pedido = new Pedido
                {
                    IdProveedor = producto.IdProveedor,
                    IdEstado = 1,
                    FechaPedido = DateOnly.FromDateTime(DateTime.Today),
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                _context.Pedidos.Add(pedido);

                await _context.SaveChangesAsync();

                pedidosPorProveedor.Add(producto.IdProveedor,pedido);
            }

            var pedidoProveedor = pedidosPorProveedor[producto.IdProveedor];

            var detalle = new PedidoDetalle
            {
                IdPedido = pedidoProveedor.IdPedido,
                IdProducto = producto.IdProducto,
                CantidadSugerida = pedidoSugerido,
                CantidadPedida = null,
                CostoUnitario = producto.CostoActual,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _context.PedidoDetalles.Add(detalle);
        }

        await _context.SaveChangesAsync();

        Console.WriteLine();
        Console.WriteLine("=================================");
        Console.WriteLine($"Productos a pedir: {productosConPedido}");
        Console.WriteLine($"Proveedores: {pedidosPorProveedor.Count}");
        Console.WriteLine("=================================");

        Console.WriteLine($"Ventas utilizadas: {importacionVentas.IdImportacion}");
        Console.WriteLine($"Fecha inicio: {importacionVentas.FechaInicioPeriodo}");
        Console.WriteLine($"Fecha fin: {importacionVentas.FechaFinPeriodo}");

        Console.WriteLine($"Dias periodo: {diasPeriodo}");

        Console.WriteLine($"Semanas analizadas: {semanasPeriodo}");
    }

    //METODO API PARA OBTENER PEDIDOS


    public async Task<List<object>> GetPedidosAsync()
    {
        return await _context.Pedidos.Select(p => new
            {
                p.IdPedido,
                Proveedor = p.Proveedor.Nombre,
                Estado = p.Estado.Nombre,
                p.FechaPedido,

                Productos = p.PedidoDetalles.Count(),

                MontoEstimado = p.PedidoDetalles.Sum(
                    d => d.CantidadSugerida * d.CostoUnitario)
            }).Cast<object>().ToListAsync();
    }

    public async Task<object?> GetPedidoAsync(int id)
    {
        return await _context.Pedidos.Where(p => p.IdPedido == id).Select(p => new
            {
                p.IdPedido,

                Proveedor = p.Proveedor.Nombre,

                Estado = p.Estado.Nombre,

                p.FechaPedido,

            Detalles = p.PedidoDetalles.Select(d => new
            {
                d.IdDetalle,

                Producto = d.Producto.Nombre,

                Existencia =_context.Inventarios.Where(i => i.IdProducto == d.IdProducto).Sum(i => (decimal?)i.Existencia) ?? 0,

                VendidoPeriodo =_context.VentasPeriodo.Where(v => v.IdProducto == d.IdProducto).Sum(v => (decimal?)v.CantidadVendida) ?? 0,

                d.CantidadSugerida,

                d.CantidadPedida,

                d.CostoUnitario,

                Subtotal =
             (
                 d.CantidadPedida ??
                 d.CantidadSugerida
             ) * d.CostoUnitario
            }),

            ProductosDisponibles = _context.Productos.Where(prod => prod.IdProveedor == p.IdProveedor && !p.PedidoDetalles.Select(d => d.IdProducto).Contains(prod.IdProducto)).Select(prod => new
                {
                    prod.IdProducto,
                    prod.Nombre,

                    Existencia = _context.Inventarios.Where(i => i.IdProducto == prod.IdProducto).Sum(i => (decimal?)i.Existencia) ?? 0,

                    VendidoPeriodo = _context.VentasPeriodo.Where(v => v.IdProducto == prod.IdProducto).Sum(v => (decimal?)v.CantidadVendida) ?? 0,

                    prod.CostoActual
                })
                .ToList(),

            MontoTotal = p.PedidoDetalles.Sum(
            d =>
                (
                    d.CantidadPedida ??
                    d.CantidadSugerida
                ) *
                d.CostoUnitario)

        })
            .FirstOrDefaultAsync();
    }

    public async Task CambiarEstadoAsync(int idPedido, int idEstado)
    {
        var pedido = await _context.Pedidos .FirstOrDefaultAsync(p => p.IdPedido == idPedido);
        if (pedido == null)
        {
            throw new Exception("Pedido no encontrado.");
        }

        var estadoExiste = await _context.EstadosPedido.AnyAsync(e => e.IdEstado == idEstado);
        if (!estadoExiste)
        {
            throw new Exception("Estado inválido.");
        }

        pedido.IdEstado = idEstado;
        pedido.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();
    }

    public async Task ActualizarCantidadAsync(int idDetalle,decimal cantidad)
    {
        var detalle = await _context.PedidoDetalles.FirstOrDefaultAsync(d => d.IdDetalle == idDetalle);

        if (detalle == null)
        {
            throw new Exception("Detalle no encontrado.");
        }

        detalle.CantidadPedida = cantidad;

        detalle.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();
    }

}