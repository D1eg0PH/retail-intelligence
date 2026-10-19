using Microsoft.EntityFrameworkCore;
using RetailIntelligence.Api.Data;
using RetailIntelligence.Api.Interfaces;
using RetailIntelligence.Api.Dtos.Producto;
using RetailIntelligence.Api.Dtos.Depuracion;

namespace RetailIntelligence.Api.Services;

public class ProductoService : IProductoService
{
    private readonly AppDbContext _context;

    public ProductoService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<List<ProductoDto>> GetAllAsync()
    {
        return await _context.Productos
            .Select(p => new ProductoDto
            {
                IdProducto = p.IdProducto,
                Codigo = p.Codigo,
                Nombre = p.Nombre,
                CostoActual = p.CostoActual,
                PrecioVentaActual = p.PrecioVentaActual,
                Proveedor = p.Proveedor.Nombre,
                Departamento = p.Departamento.Nombre
            })
            .ToListAsync();
    }


    public async Task<DepuracionProductosResponseDto> GetDepuracionAsync()
    {
        // =========================================
        // 1. OBTENER LA CONFIGURACIÓN
        // =========================================

        var configuracion = await _context.ConfiguracionSistema
            .FirstOrDefaultAsync();

        if (configuracion == null)
        {
            throw new Exception(
                "No existe una configuración del sistema.");
        }

        var diasProductoMuerto = configuracion.DiasProductoMuerto;

        // Las fechas de importación son DateOnly. El análisis termina
        // en la última fecha de ventas válida disponible, no necesariamente hoy.
        var hoy = DateOnly.FromDateTime(DateTime.Today);
        var importacionesVentas = await _context.Importaciones
            .Where(i => i.TipoArchivo == "VENTAS_PERIODO")
            .OrderBy(i => i.FechaInicioPeriodo)
            .ToListAsync();

        var ultimaFechaVentas = importacionesVentas
            .Where(i =>
                i.FechaInicioPeriodo != default &&
                i.FechaFinPeriodo != default &&
                i.FechaInicioPeriodo <= i.FechaFinPeriodo &&
                i.FechaFinPeriodo <= hoy)
            .OrderByDescending(i => i.FechaFinPeriodo)
            .ThenByDescending(i => i.FechaInicioPeriodo)
            .ThenByDescending(i => i.CreatedAt)
            .Select(i => (DateOnly?)i.FechaFinPeriodo)
            .FirstOrDefault();

        var fechaFin = ultimaFechaVentas ?? hoy;
        var fechaInicioObjetivo = fechaFin.AddDays(-(diasProductoMuerto - 1));

        // =========================================
        // 2. OBTENER EL INVENTARIO MÁS RECIENTE
        // =========================================

        var importacionInventario = await _context.Importaciones
            .Where(i => i.TipoArchivo == "INVENTARIO")
            .OrderByDescending(i => i.CreatedAt)
            .FirstOrDefaultAsync();

        var inventarioPorProducto = new Dictionary<int, decimal>();

        if (importacionInventario != null)
        {
            inventarioPorProducto = await _context.Inventarios
                .Where(i =>
                    i.IdImportacion ==
                    importacionInventario.IdImportacion)
                .GroupBy(i => i.IdProducto)
                .Select(g => new
                {
                    IdProducto = g.Key,
                    Existencia = g.Sum(i => i.Existencia)
                })
                .ToDictionaryAsync(
                    x => x.IdProducto,
                    x => x.Existencia);
        }

        // =========================================
        // 3. OBTENER TODAS LAS IMPORTACIONES DE VENTAS
        // =========================================

        var importacionesEnVentana = importacionesVentas
            .Where(i =>
                i.FechaInicioPeriodo <= i.FechaFinPeriodo &&
                i.FechaInicioPeriodo <= fechaFin &&
                i.FechaFinPeriodo >= fechaInicioObjetivo &&
                i.FechaFinPeriodo <= fechaFin)
            .ToList();

        var fechaInicio = importacionesEnVentana.Count == 0
            ? fechaInicioObjetivo
            : importacionesEnVentana.Min(i => i.FechaInicioPeriodo) < fechaInicioObjetivo
                ? importacionesEnVentana.Min(i => i.FechaInicioPeriodo)
                : fechaInicioObjetivo;
        var diasPeriodoAnalizado =
            fechaFin.DayNumber - fechaInicio.DayNumber + 1;

        // =========================================
        // 4. DETERMINAR SI EXISTE COBERTURA COMPLETA
        // =========================================

        var coberturaCompleta = false;
        var diasCubiertos = 0;

        if (importacionesEnVentana.Count > 0)
        {
            var periodos = importacionesEnVentana
                .Select(i => new
                {
                    Inicio = i.FechaInicioPeriodo < fechaInicio
                        ? fechaInicio
                        : i.FechaInicioPeriodo,

                    Fin = i.FechaFinPeriodo > fechaFin
                        ? fechaFin
                        : i.FechaFinPeriodo
                })
                .OrderBy(p => p.Inicio)
                .ThenBy(p => p.Fin)
                .ToList();

            var finCoberturaContada = (DateOnly?)null;

            foreach (var periodo in periodos)
            {
                if (finCoberturaContada == null)
                {
                    diasCubiertos += periodo.Fin.DayNumber -
                        periodo.Inicio.DayNumber + 1;
                    finCoberturaContada = periodo.Fin;
                    continue;
                }

                if (periodo.Fin <= finCoberturaContada.Value)
                {
                    continue;
                }

                var inicioDiasNuevos = periodo.Inicio > finCoberturaContada.Value
                    ? periodo.Inicio
                    : finCoberturaContada.Value.AddDays(1);
                diasCubiertos += periodo.Fin.DayNumber -
                    inicioDiasNuevos.DayNumber + 1;
                finCoberturaContada = periodo.Fin;
            }

            coberturaCompleta = diasCubiertos == diasPeriodoAnalizado &&
                diasPeriodoAnalizado >= diasProductoMuerto;
        }

        // =========================================
        // 5. SUMAR LAS VENTAS DE LOS PERÍODOS
        //    QUE SE CRUZAN CON LA VENTANA
        // =========================================

        var ventasPorProducto = new Dictionary<int, decimal>();

        if (importacionesEnVentana.Count > 0)
        {
            var idsImportaciones = importacionesEnVentana
                .Select(i => i.IdImportacion)
                .ToList();

            ventasPorProducto = await _context.VentasPeriodo
                .Where(v => idsImportaciones.Contains(v.IdImportacion))
                .GroupBy(v => v.IdProducto)
                .Select(g => new
                {
                    IdProducto = g.Key,
                    CantidadVendida = g.Sum(v => v.CantidadVendida)
                })
                .ToDictionaryAsync(
                    x => x.IdProducto,
                    x => x.CantidadVendida);
        }

        // =========================================
        // 6. OBTENER LOS PRODUCTOS
        // =========================================

        var productos = await _context.Productos
            .Select(p => new
            {
                p.IdProducto,
                p.Codigo,
                p.Nombre,
                Proveedor = p.Proveedor.Nombre,
                p.CostoActual
            })
            .ToListAsync();

        // =========================================
        // 7. CLASIFICAR Y CONSTRUIR LA RESPUESTA
        // =========================================

        var productosDepuracion = productos
            .Select(p =>
            {
                var existencia = inventarioPorProducto
                    .GetValueOrDefault(p.IdProducto, 0m);

                var vendidoPeriodo = ventasPorProducto
                    .GetValueOrDefault(p.IdProducto, 0m);

                string clasificacion;

                if (importacionesVentas.Count == 0)
                {
                    clasificacion = "Sin datos de ventas";
                }
                else if (vendidoPeriodo > 0)
                {
                    clasificacion = "Con movimiento";
                }
                else if (coberturaCompleta)
                {
                    clasificacion = "Posible producto muerto";
                }
                else
                {
                    clasificacion = "Información insuficiente";
                }

                return new ProductoDepuracionDto
                {
                    IdProducto = p.IdProducto,
                    Codigo = p.Codigo,
                    Nombre = p.Nombre,
                    Proveedor = p.Proveedor,
                    Existencia = existencia,
                    CostoActual = p.CostoActual,
                    ValorInventario = existencia * p.CostoActual,
                    VendidoPeriodo = vendidoPeriodo,
                    DiasPeriodo = diasPeriodoAnalizado,
                    Clasificacion = clasificacion
                };
            })
            .OrderBy(p => p.Clasificacion)
            .ThenByDescending(p => p.ValorInventario)
            .ToList();

        return new DepuracionProductosResponseDto
        {
            FechaInicioAnalisis = fechaInicio,
            FechaFinAnalisis = fechaFin,
            DiasPeriodo = diasPeriodoAnalizado,
            DiasObjetivo = diasProductoMuerto,
            DiasCubiertos = diasCubiertos,
            CoberturaCompleta = coberturaCompleta,
            FuenteInventario = importacionInventario == null
                ? null
                : new FuenteInventarioDepuracionDto
                {
                    NombreArchivo = importacionInventario.NombreArchivo,
                    FechaInventario = importacionInventario.FechaFinPeriodo,
                    RegistrosImportados = importacionInventario.RegistrosImportados,
                    ImportadoEl = importacionInventario.CreatedAt
                },
            PeriodosVentas = importacionesEnVentana
                .Select(i => new PeriodoVentasDepuracionDto
                {
                    NombreArchivo = i.NombreArchivo,
                    FechaInicio = i.FechaInicioPeriodo,
                    FechaFin = i.FechaFinPeriodo,
                    RegistrosImportados = i.RegistrosImportados,
                    ImportadoEl = i.CreatedAt
                })
                .ToList(),
            Productos = productosDepuracion
        };
    }
}
