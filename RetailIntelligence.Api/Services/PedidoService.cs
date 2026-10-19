using Microsoft.EntityFrameworkCore;
using RetailIntelligence.Api.Data;
using RetailIntelligence.Api.Entities;
using RetailIntelligence.Api.Interfaces;
using RetailIntelligence.Api.Dtos;
using RetailIntelligence.Api.Dtos.Pedido;

namespace RetailIntelligence.Api.Services;

public class PedidoService : IPedidoService
{
    private readonly AppDbContext _context;

    public PedidoService(AppDbContext context)
    {
        _context = context;
    }

    private async Task LimpiarPedidos()
    {
        await _context.Database.ExecuteSqlRawAsync(
            "TRUNCATE TABLE pedidodetalle RESTART IDENTITY CASCADE");

        await _context.Database.ExecuteSqlRawAsync(
            "TRUNCATE TABLE pedidos RESTART IDENTITY CASCADE");
    }

    public async Task GenerarPedidosAsync()
    {
        var importacionInventario =
            await ObtenerUltimaImportacionInventario();

        var importacionVentas =
            await ObtenerUltimaImportacionVentas();

        var ventasPorProducto =
            await ObtenerVentasPorProducto(importacionVentas);

        var inventarioActual =
            await ObtenerInventarioActual(importacionInventario);

        var diasPeriodo =
            importacionVentas.FechaFinPeriodo.DayNumber -
            importacionVentas.FechaInicioPeriodo.DayNumber + 1;

        if (importacionVentas.FechaInicioPeriodo == default ||
            importacionVentas.FechaFinPeriodo == default ||
            diasPeriodo <= 0)
        {
            throw new InvalidOperationException(
                "El período de la importación de ventas es inválido.");
        }

        var configuracion = await ObtenerConfiguracionAsync();

        await using var transaccion =
            await _context.Database.BeginTransactionAsync();

        try
        {
            await LimpiarPedidos();

            await CrearPedidos(
                ventasPorProducto,
                inventarioActual,
                importacionVentas,
                diasPeriodo,
                configuracion.PorcentajeColchon);

            await transaccion.CommitAsync();
        }
        catch
        {
            await transaccion.RollbackAsync();
            _context.ChangeTracker.Clear();
            throw;
        }
    }

    private async Task<Importacion> ObtenerUltimaImportacionInventario()
    {
        var importacion = await _context.Importaciones
            .Where(i => i.TipoArchivo == "INVENTARIO")
            .OrderByDescending(i => i.CreatedAt)
            .FirstOrDefaultAsync();

        if (importacion == null)
        {
            throw new InvalidOperationException(
                "No existe ninguna importación de inventario.");
        }

        return importacion;
    }

    private async Task<List<VentaProductoDto>> ObtenerVentasPorProducto(Importacion importacionVentas)
    {
        var ventas = await _context.VentasPeriodo
            .Where(v => v.IdImportacion == importacionVentas.IdImportacion)
            .GroupBy(v => v.IdProducto)
            .Select(g => new VentaProductoDto
            {
                IdProducto = g.Key,
                CantidadVendida = g.Sum(x => x.CantidadVendida)
            })
            .ToListAsync();

        Console.WriteLine($"Productos con ventas: {ventas.Count}");

        return ventas;
    }


    private async Task<Importacion> ObtenerUltimaImportacionVentas()
    {
        var importacion = await _context.Importaciones
            .Where(i => i.TipoArchivo == "VENTAS_PERIODO")
            .OrderByDescending(i => i.FechaFinPeriodo)
            .ThenByDescending(i => i.FechaInicioPeriodo)
            .ThenByDescending(i => i.CreatedAt)
            .FirstOrDefaultAsync();

        if (importacion == null)
        {
            throw new InvalidOperationException(
                "No existe ninguna importación de ventas.");
        }

        var diasPeriodo = importacion.FechaFinPeriodo.DayNumber -
            importacion.FechaInicioPeriodo.DayNumber + 1;

        if (importacion.FechaInicioPeriodo == default ||
            importacion.FechaFinPeriodo == default ||
            diasPeriodo <= 0)
        {
            throw new InvalidOperationException(
                "El período de la última importación de ventas es inválido.");
        }

        return importacion;
    }

    private async Task<ConfiguracionSistema> ObtenerConfiguracionAsync()
    {
        var configuracion = await _context.ConfiguracionSistema
            .SingleOrDefaultAsync();

        if (configuracion == null)
        {
            throw new InvalidOperationException(
                "No existe una configuración del sistema para generar pedidos.");
        }

        if (configuracion.PorcentajeColchon < 0m)
        {
            throw new InvalidOperationException(
                "El porcentaje de colchón configurado no puede ser negativo.");
        }

        return configuracion;
    }


    private async Task<Dictionary<int, decimal>> ObtenerInventarioActual(Importacion importacionInventario)
    {
        return await _context.Inventarios
            .Where(i =>
                i.IdImportacion == importacionInventario.IdImportacion)
            .GroupBy(i => i.IdProducto)
            .ToDictionaryAsync(
                g => g.Key,
                g => g.Sum(x => x.Existencia));
    }

    private async Task CrearPedidos(
        List<VentaProductoDto> ventasPorProducto,
        Dictionary<int, decimal> inventarioActual,
        Importacion importacionVentas,
        int diasPeriodo,
        decimal porcentajeColchon)
    {
        var semanasPeriodo = diasPeriodo / 7m;
        var factorColchon = 1m + porcentajeColchon / 100m;

        Console.WriteLine($"Colchón configurado: {porcentajeColchon}%");
        Console.WriteLine($"Importación utilizada: {importacionVentas.IdImportacion}");
        Console.WriteLine(
            $"Período analizado: {importacionVentas.FechaInicioPeriodo} al " +
            $"{importacionVentas.FechaFinPeriodo} ({diasPeriodo} días, " +
            $"{semanasPeriodo} semanas)");

        var pedidosPorProveedor = new Dictionary<int, Pedido>();
        var productos = await _context.Productos
            .ToDictionaryAsync(p => p.IdProducto);
        var ahora = DateTime.UtcNow;
        var productosConPedido = 0;

        foreach (var venta in ventasPorProducto)
        {
            if (!productos.TryGetValue(venta.IdProducto, out var producto) ||
                producto.IdProveedor == 3)
            {
                continue;
            }

            var promedioSemanal = venta.CantidadVendida / semanasPeriodo;
            var demandaObjetivo = promedioSemanal * factorColchon;
            var existenciaActual = inventarioActual.GetValueOrDefault(
                venta.IdProducto,
                0m);
            var pedidoSugerido = Math.Max(
                0m,
                Math.Ceiling(demandaObjetivo - existenciaActual));

            if (pedidoSugerido <= 0m)
            {
                continue;
            }

            if (!pedidosPorProveedor.TryGetValue(
                    producto.IdProveedor,
                    out var pedidoProveedor))
            {
                pedidoProveedor = new Pedido
                {
                    IdProveedor = producto.IdProveedor,
                    IdEstado = 1,
                    FechaPedido = DateOnly.FromDateTime(DateTime.Today),
                    CreatedAt = ahora,
                    UpdatedAt = ahora
                };

                pedidosPorProveedor.Add(producto.IdProveedor, pedidoProveedor);
                _context.Pedidos.Add(pedidoProveedor);
            }

            pedidoProveedor.PedidoDetalles.Add(new PedidoDetalle
            {
                IdProducto = producto.IdProducto,
                CantidadSugerida = pedidoSugerido,
                CantidadPedida = null,
                CostoUnitario = producto.CostoActual,
                CreatedAt = ahora,
                UpdatedAt = ahora
            });

            productosConPedido++;
        }

        await _context.SaveChangesAsync();

        Console.WriteLine($"Productos con pedido: {productosConPedido}");
        Console.WriteLine($"Proveedores con pedido: {pedidosPorProveedor.Count}");
    }


    public async Task<List<PedidoDto>> GetPedidosAsync()
    {
            return await _context.Pedidos
        .Select(p => new PedidoDto
        {
            IdPedido = p.IdPedido,
            Proveedor = p.Proveedor.Nombre,
            Estado = p.Estado.Nombre,
            FechaPedido = p.FechaPedido,
            Productos = p.PedidoDetalles.Count(),
            MontoEstimado = p.PedidoDetalles.Sum(
                d => d.CantidadSugerida * d.CostoUnitario)
        })
        .ToListAsync();
    }

    public async Task<PedidoCompletoDto?> GetPedidoAsync(int id)
    {
        return await _context.Pedidos
            .Where(p => p.IdPedido == id)
            .Select(p => new PedidoCompletoDto
            {
                IdPedido = p.IdPedido,

                Proveedor = p.Proveedor.Nombre,

                Estado = p.Estado.Nombre,

                FechaPedido = p.FechaPedido,

                Detalles = p.PedidoDetalles
                    .Select(d => new PedidoDetalleDto
                    {
                        IdDetalle = d.IdDetalle,

                        Producto = d.Producto.Nombre,

                        Existencia = _context.Inventarios
                            .Where(i => i.IdProducto == d.IdProducto)
                            .Sum(i => (decimal?)i.Existencia) ?? 0,

                        VendidoPeriodo = _context.VentasPeriodo
                            .Where(v => v.IdProducto == d.IdProducto)
                            .Sum(v => (decimal?)v.CantidadVendida) ?? 0,

                        CantidadSugerida = d.CantidadSugerida,

                        CantidadPedida = d.CantidadPedida,

                        CostoUnitario = d.CostoUnitario,

                        Subtotal =
                            (d.CantidadPedida ?? d.CantidadSugerida)
                            * d.CostoUnitario
                    })
                    .ToList(),

                ProductosDisponibles = _context.Productos
                    .Where(prod =>
                        prod.IdProveedor == p.IdProveedor &&
                        !p.PedidoDetalles
                            .Select(d => d.IdProducto)
                            .Contains(prod.IdProducto))
                    .Select(prod => new ProductoDisponibleDto
                    {
                        IdProducto = prod.IdProducto,

                        Nombre = prod.Nombre,

                        Existencia = _context.Inventarios
                            .Where(i => i.IdProducto == prod.IdProducto)
                            .Sum(i => (decimal?)i.Existencia) ?? 0,

                        VendidoPeriodo = _context.VentasPeriodo
                            .Where(v => v.IdProducto == prod.IdProducto)
                            .Sum(v => (decimal?)v.CantidadVendida) ?? 0,

                        CostoActual = prod.CostoActual
                    })
                    .ToList(),

                MontoTotal = p.PedidoDetalles.Sum(d =>
                    (d.CantidadPedida ?? d.CantidadSugerida)
                    * d.CostoUnitario)
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

    public async Task AgregarProductoAsync(int idPedido,int idProducto)
    {
        var pedido = await _context.Pedidos
            .FirstOrDefaultAsync(
                p => p.IdPedido == idPedido);

        if (pedido == null)
        {
            throw new Exception(
                "Pedido no encontrado.");
        }

        var producto = await _context.Productos
            .FirstOrDefaultAsync(
                p => p.IdProducto == idProducto);

        if (producto == null)
        {
            throw new Exception(
                "Producto no encontrado.");
        }

        var yaExiste = await _context.PedidoDetalles
            .AnyAsync(d =>
                d.IdPedido == idPedido &&
                d.IdProducto == idProducto);

        if (yaExiste)
        {
            throw new Exception(
                "El producto ya está agregado.");
        }

        var detalle = new PedidoDetalle
        {
            IdPedido = idPedido,
            IdProducto = idProducto,

            CantidadSugerida = 0,

            // por comodidad iniciamos con 1
            CantidadPedida = 1,

            CostoUnitario =
                producto.CostoActual,

            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.PedidoDetalles.Add(
            detalle);

        await _context.SaveChangesAsync();
    }

    public async Task QuitarProductosAsync(List<int> detalles)
    {
        var registros = await _context.PedidoDetalles
            .Where(d => detalles.Contains(d.IdDetalle))
            .ToListAsync();

        _context.PedidoDetalles.RemoveRange(registros);

        await _context.SaveChangesAsync();
    }

    public async Task ActualizarCantidadesAsync(int idPedido,List<ActualizarCantidadesDto> cantidades)
    {
        var detalles = await _context.PedidoDetalles.Where(x => x.IdPedido == idPedido).ToListAsync();

        foreach (var detalle in detalles)
        {
            var dto = cantidades.FirstOrDefault(
                x => x.IdDetalle == detalle.IdDetalle);

            if (dto != null)
            {
                detalle.CantidadPedida = dto.CantidadPedida;
                detalle.UpdatedAt = DateTime.UtcNow;
            }
        }

        await _context.SaveChangesAsync();
    }

}
