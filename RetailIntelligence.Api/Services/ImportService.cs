using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using RetailIntelligence.Api.Data;
using RetailIntelligence.Api.Interfaces;
using RetailIntelligence.Api.Entities;
using RetailIntelligence.Api.DTOs;
using System.Globalization;


namespace RetailIntelligence.Api.Services;

public class ImportService : IImportService
{
    // Contexto de Entity Framework para acceder a PostgreSQL
    private readonly AppDbContext _context;

    // Inyección de dependencias del contexto
    public ImportService(AppDbContext context)
    {
        _context = context;
    }

    private static decimal LeerDecimal(IXLCell cell)
    {
        if (cell.DataType == XLDataType.Number)
        {
            return cell.GetValue<decimal>();
        }

        var texto = cell.GetString()
            .Replace("$", "")
            .Replace(",", "")
            .Trim();

        if (decimal.TryParse(
            texto,
            NumberStyles.Any,
            CultureInfo.InvariantCulture,
            out var valor))
        {
            return valor;
        }

        throw new Exception(
            $"No fue posible convertir el valor '{cell.GetString()}' de la celda {cell.Address} a decimal.");
    }


    // IMPORTAR INVENTARIO, PRODUCTOS, PROVEEDORES

    public async Task ImportInventoryAsync(IFormFile file)
    {
        // Abrir el archivo Excel recibido
        using var stream = file.OpenReadStream();

        // Cargar el archivo usando ClosedXML
        using var workbook = new XLWorkbook(stream);

        // Obtener la primera hoja del archivo
        var worksheet = workbook.Worksheet(1);

        Console.WriteLine($"Hoja encontrada: {worksheet.Name}");

        await _context.Inventarios.ExecuteDeleteAsync();

        await _context.Importaciones.Where(i => i.TipoArchivo == "INVENTARIO").ExecuteDeleteAsync();

        // ==========================
        // VALIDACIÓN DE ENCABEZADOS
        // ==========================

        // Encabezados que esperamos recibir desde Eleventa
        var expectedHeaders = new List<string>
            {
                "Código",
                "Producto",
                "P. Costo",
                "P. Venta",
                "P. Mayoreo",
                "Existencia",
                "Inv. Mínimo",
                "Inv. Máximo",
                "Departamento"
            };

        // Leer encabezados reales del Excel
        var actualHeaders = worksheet.Row(1).CellsUsed()
            .Select(c => c.GetString().Trim())
            .ToList();

        // Verificar que existan todas las columnas necesarias
        foreach (var expectedHeader in expectedHeaders)
        {
            if (!actualHeaders.Contains(expectedHeader))
            {
                throw new Exception(
                    $"El archivo no contiene la columna requerida: {expectedHeader}");
            }
        }

        // ====================================
        // OBTENER FILAS DE PRODUCTOS
        // ====================================

        // Saltamos la fila 1 porque contiene encabezados
        var rows = worksheet.RowsUsed().Skip(1);

        // ====================================
        // OBTENER PROVEEDORES ÚNICOS
        // ====================================

        var proveedoresExcel = rows
            .Select(r => r.Cell(9).GetString().Trim())
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .Distinct()
            .ToList();
        // Revisar si los proveedores existen en PostgreSQL
        foreach (var nombreProveedor in proveedoresExcel)
        {
            var existe = await _context.Proveedores
                .AnyAsync(p => p.Nombre == nombreProveedor);

            if (existe)
            {
                Console.WriteLine($"{nombreProveedor} -> EXISTE");
                continue;
            }

            Console.WriteLine($"Creando proveedor: {nombreProveedor}");

            var nuevoProveedor = new Proveedor
            {
                Nombre = nombreProveedor,
                Activo = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _context.Proveedores.Add(nuevoProveedor);
        }

        // Guardar todos los proveedores nuevos de una sola vez
        await _context.SaveChangesAsync();




        // ====================================
        // RECORRER PRODUCTOS DEL EXCEL
        // ====================================

        var hoy = DateOnly.FromDateTime(DateTime.Today);

        var importacion = new Importacion
        {
            TipoArchivo = "INVENTARIO",
            NombreArchivo = file.FileName,
            FechaInicioPeriodo = hoy,
            FechaFinPeriodo = hoy,
            HashArchivo = Guid.NewGuid().ToString(),
            RegistrosImportados = rows.Count(),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.Importaciones.Add(importacion);

        await _context.SaveChangesAsync();


        foreach (var row in rows)
        {
            var codigo = row.Cell(1).GetString();
            var producto = row.Cell(2).GetString();
            Console.WriteLine($"Fila {row.RowNumber()} | Costo='{row.Cell(3).GetString()}' | Tipo={row.Cell(3).DataType}");
            var costo = LeerDecimal(row.Cell(3));
            var precio = LeerDecimal(row.Cell(4));
            var existencia = LeerDecimal(row.Cell(6));
            var stockMinimoExcel = LeerDecimal(row.Cell(7));
            var stockMaximoExcel = LeerDecimal(row.Cell(8));
            //var departamento = row.Cell(9).GetString().Trim();
            var proveedor = row.Cell(9).GetString().Trim();
            if (stockMaximoExcel < stockMinimoExcel)
            {
                Console.WriteLine(
                    $"Eleventa tiene configuración inválida: {codigo} | Min:{stockMinimoExcel} Max:{stockMaximoExcel}");
            }

            var proveedorDb = await _context.Proveedores.FirstAsync(p => p.Nombre == proveedor);

            var idProveedor = proveedorDb.IdProveedor;

            var productoExistente = await _context.Productos.FirstOrDefaultAsync(p => p.Codigo == codigo);

            int idProducto;

            if (productoExistente == null)
            {
                Console.WriteLine($"Creando producto: {codigo} - {producto}");

                var nuevoProducto = new Producto
                {
                    Codigo = codigo,
                    Nombre = producto,

                    CostoActual = costo,
                    PrecioVentaActual = precio,

                    IdProveedor = idProveedor,

                    // Temporal hasta que exista clasificación IA
                    IdDepartamento = 1,

                    // Temporal hasta implementar el motor de reposición
                    StockMinimo = 0,
                    StockMaximo = 0,

                    Activo = true,
                    EsEsencial = false,
                    Descontinuado = false,

                    UnidadMedida = "PZA",

                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                _context.Productos.Add(nuevoProducto);

                // Necesitamos guardar para obtener el ID generado por PostgreSQL
                await _context.SaveChangesAsync();

                idProducto = nuevoProducto.IdProducto;
            }
            else
            {
                Console.WriteLine($"Producto existente: {codigo}");

                productoExistente.Nombre = producto;
                productoExistente.CostoActual = costo;
                productoExistente.PrecioVentaActual = precio;

                productoExistente.IdProveedor = idProveedor;

                // Temporal
                productoExistente.IdDepartamento = 1;

                productoExistente.UpdatedAt = DateTime.UtcNow;

                idProducto = productoExistente.IdProducto;
            }

            var inventario = new Inventario
            {
                IdProducto = idProducto,
                IdImportacion = importacion.IdImportacion,
                Existencia = existencia,
                PrecioCosto = costo,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _context.Inventarios.Add(inventario);


        }

        await _context.SaveChangesAsync();

    }


    // IMPORTAR VENTAS POR PERIODO
    public async Task ImportSalesAsync(ImportSalesRequest request)
    {
        // =========================================
        // 1. VALIDAR EL PERIODO
        // =========================================

        if (request.DiasPeriodo.HasValue &&
            (request.FechaInicio.HasValue || request.FechaFin.HasValue))
        {
            throw new Exception(
                "Debe seleccionar un solo tipo de período: días o rango de fechas.");
        }

        DateOnly fechaInicio;
        DateOnly fechaFin;

        if (request.DiasPeriodo.HasValue)
        {
            if (request.DiasPeriodo.Value <= 0)
            {
                throw new Exception(
                    "Los días del período deben ser mayores a cero.");
            }

            fechaFin = DateOnly.FromDateTime(DateTime.Today);
            fechaInicio = fechaFin.AddDays(
                -(request.DiasPeriodo.Value - 1));
        }
        else
        {
            if (!request.FechaInicio.HasValue ||
                !request.FechaFin.HasValue)
            {
                throw new Exception(
                    "Debe indicar un período de ventas.");
            }

            fechaInicio = request.FechaInicio.Value;
            fechaFin = request.FechaFin.Value;

            if (fechaInicio > fechaFin)
            {
                throw new Exception(
                    "La fecha inicial no puede ser mayor que la final.");
            }
        }

        // =========================================
        // 2. LEER Y VALIDAR EL EXCEL
        // =========================================

        using var stream = request.File.OpenReadStream();
        using var workbook = new XLWorkbook(stream);

        var worksheet = workbook.Worksheet(1);

        var expectedHeaders = new List<string>
        {
            "Código",
            "Descripción",
            "Cantidad",
            "Precio Usado",
            "Precio Costo",
            "Importe",
            "Departamento"
        };

        var actualHeaders = worksheet.Row(1)
            .CellsUsed()
            .Select(c => c.GetString().Trim())
            .ToList();

        foreach (var expectedHeader in expectedHeaders)
        {
            if (!actualHeaders.Contains(expectedHeader))
            {
                throw new Exception(
                    $"El archivo no contiene la columna requerida: {expectedHeader}");
            }
        }

        var rows = worksheet.RowsUsed()
            .Skip(1)
            .Where(r => !r.IsEmpty())
            .ToList();

        if (rows.Count == 0)
        {
            throw new Exception(
                "El archivo no contiene registros de ventas.");
        }

        // =========================================
        // 3. PREPARAR LOS DATOS ANTES DE MODIFICAR
        //    LA BASE DE DATOS
        // =========================================

        var productos = await _context.Productos
            .Select(p => new
            {
                p.IdProducto,
                p.Codigo
            })
            .ToListAsync();

        var productosPorCodigo = productos
            .GroupBy(p => p.Codigo)
            .ToDictionary(
                g => g.Key,
                g => g.First().IdProducto);

        var ventasPreparadas =
            new List<(
                int IdProducto,
                decimal Cantidad,
                decimal PrecioUsado,
                decimal PrecioCosto,
                decimal Importe)>();

        foreach (var row in rows)
        {
            var codigo = row.Cell(1).GetString().Trim();

            var cantidad = LeerDecimal(row.Cell(3));
            var precioUsado = LeerDecimal(row.Cell(4));
            var precioCosto = LeerDecimal(row.Cell(5));
            var importe = LeerDecimal(row.Cell(6));

            if (!productosPorCodigo.TryGetValue(
                codigo, out var idProducto))
            {
                Console.WriteLine(
                    $"Producto no encontrado en catálogo: {codigo}");

                continue;
            }

            ventasPreparadas.Add((
                idProducto,
                cantidad,
                precioUsado,
                precioCosto,
                importe));
        }

        if (ventasPreparadas.Count == 0)
        {
            throw new Exception(
                "No se encontraron productos del archivo en el catálogo. " +
                "No se modificó ninguna importación existente.");
        }

        // =========================================
        // 4. COMPROBAR TRASLAPES Y GUARDAR
        // =========================================

        await using var transaction =
            await _context.Database.BeginTransactionAsync();

        try
        {
            var importacionesExistentes = await _context.Importaciones
                .Where(i => i.TipoArchivo == "VENTAS_PERIODO")
                .ToListAsync();

            // Dos periodos se traslapan si comparten al menos un día.
            var traslapes = importacionesExistentes
                .Where(i =>
                    fechaInicio <= i.FechaFinPeriodo &&
                    fechaFin >= i.FechaInicioPeriodo)
                .ToList();

            // Los traslapes parciales siempre se bloquean.
            var traslapesParciales = traslapes
                .Where(i =>
                    i.FechaInicioPeriodo != fechaInicio ||
                    i.FechaFinPeriodo != fechaFin)
                .ToList();

            if (traslapesParciales.Count > 0)
            {
                var periodosConflicto = string.Join(
                    "; ",
                    traslapesParciales.Select(i =>
                        $"{i.FechaInicioPeriodo:dd/MM/yyyy} al " +
                        $"{i.FechaFinPeriodo:dd/MM/yyyy}"));

                throw new Exception(
                    $"No se puede importar el período " +
                    $"{fechaInicio:dd/MM/yyyy} al {fechaFin:dd/MM/yyyy} " +
                    $"porque se traslapa con estas importaciones existentes: " +
                    $"{periodosConflicto}. " +
                    "Modifica las fechas para evitar traslapes.");
            }

            // Si las fechas coinciden exactamente, reemplazar
            // solamente las importaciones de ese mismo periodo.
            if (traslapes.Count > 0)
            {
                var idsImportacionesAnteriores = traslapes
                    .Select(i => i.IdImportacion)
                    .ToList();

                await _context.VentasPeriodo
                    .Where(v =>
                        idsImportacionesAnteriores.Contains(v.IdImportacion))
                    .ExecuteDeleteAsync();

                await _context.Importaciones
                    .Where(i =>
                        idsImportacionesAnteriores.Contains(i.IdImportacion))
                    .ExecuteDeleteAsync();

                Console.WriteLine(
                    $"Se reemplazará la importación anterior del período " +
                    $"{fechaInicio:dd/MM/yyyy} al {fechaFin:dd/MM/yyyy}.");
            }

            // Crear la nueva importación.
            var importacion = new Importacion
            {
                TipoArchivo = "VENTAS_PERIODO",
                NombreArchivo = request.File.FileName,
                FechaInicioPeriodo = fechaInicio,
                FechaFinPeriodo = fechaFin,
                HashArchivo = Guid.NewGuid().ToString(),
                RegistrosImportados = rows.Count,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _context.Importaciones.Add(importacion);

            await _context.SaveChangesAsync();

            // Guardar las ventas vinculadas a esta importación.
            var ahora = DateTime.UtcNow;

            foreach (var ventaPreparada in ventasPreparadas)
            {
                var venta = new VentaPeriodo
                {
                    IdProducto = ventaPreparada.IdProducto,
                    IdImportacion = importacion.IdImportacion,
                    CantidadVendida = ventaPreparada.Cantidad,
                    PrecioUsado = ventaPreparada.PrecioUsado,
                    PrecioCosto = ventaPreparada.PrecioCosto,
                    Importe = ventaPreparada.Importe,
                    CreatedAt = ahora,
                    UpdatedAt = ahora
                };

                _context.VentasPeriodo.Add(venta);
            }

            await _context.SaveChangesAsync();

            await transaction.CommitAsync();

            Console.WriteLine(
                $"Importación completada. " +
                $"Período: {fechaInicio:dd/MM/yyyy} al {fechaFin:dd/MM/yyyy}. " +
                $"Filas del Excel: {rows.Count}. " +
                $"Ventas guardadas: {ventasPreparadas.Count}.");
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

}





    /*
        public async Task ImportSalesAsync(IFormFile file,DateOnly fechaInicio,DateOnly fechaFin)
        {

            if (fechaInicio == default || fechaFin == default)
            {
                throw new Exception("Periodo de ventas inválido.");
            }

            if (fechaInicio > fechaFin)
            {
                throw new Exception("La fecha inicial no puede ser mayor a la fecha final.");
            }

            using var stream = file.OpenReadStream();

            using var workbook = new XLWorkbook(stream);

            var worksheet = workbook.Worksheet(1);

            var expectedHeaders = new List<string>
                {
                    "Código",
                    "Descripción",
                    "Cantidad",
                    "Precio Usado",
                    "Precio Costo",
                    "Importe",
                    "Departamento"
                };

            var actualHeaders = worksheet.Row(1).CellsUsed().Select(c => c.GetString().Trim()).ToList();

            foreach (var expectedHeader in expectedHeaders)
            {
                if (!actualHeaders.Contains(expectedHeader))
                {
                    throw new Exception($"El archivo no contiene la columna requerida: {expectedHeader}");
                }
            }

            Console.WriteLine("Encabezados válidos.");

            var rows = worksheet.RowsUsed().Skip(1);

            Console.WriteLine($"Registros encontrados: {rows.Count()}");


            await _context.VentasPeriodo.ExecuteDeleteAsync();

            await _context.Importaciones
                .Where(i => i.TipoArchivo == "VENTAS_PERIODO")
                .ExecuteDeleteAsync();


            var importacion = new Importacion
            {
                TipoArchivo = "VENTAS_PERIODO",
                NombreArchivo = file.FileName,
                FechaInicioPeriodo = fechaInicio,
                FechaFinPeriodo = fechaFin,
                HashArchivo = Guid.NewGuid().ToString(),
                RegistrosImportados = rows.Count(),
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _context.Importaciones.Add(importacion);

            await _context.SaveChangesAsync();


            foreach (var row in rows)
            {
                var codigo = row.Cell(1).GetString().Trim();

                var cantidad = row.Cell(3).GetValue<decimal>();

                var precioUsado = row.Cell(4).GetValue<decimal>();

                var precioCosto = row.Cell(5).GetValue<decimal>();

                var importe = row.Cell(6).GetValue<decimal>();


                // DEBUG TEMPORAL
                Console.WriteLine(
                    $"COD:{codigo} | " +
                    $"CANT:{cantidad} | " +
                    $"PRECIO:{precioUsado} | " +
                    $"IMPORTE:{importe}");

                // Buscar producto previamente importado desde inventario
                var producto = await _context.Productos.FirstOrDefaultAsync(p => p.Codigo == codigo);

                if (producto == null)
                {
                    Console.WriteLine($"Producto no encontrado en catálogo: {codigo}");
                    continue;
                }

                var venta = new VentaPeriodo
                {
                    IdProducto = producto.IdProducto,

                    IdImportacion = importacion.IdImportacion,

                    CantidadVendida = cantidad,

                    PrecioUsado = precioUsado,

                    PrecioCosto = precioCosto,

                    Importe = importe,

                    CreatedAt = DateTime.UtcNow,

                    UpdatedAt = DateTime.UtcNow
                };

                _context.VentasPeriodo.Add(venta);
            }

            await _context.SaveChangesAsync();

            Console.WriteLine(
                $"Ventas importadas correctamente: {rows.Count()}");

        }

    */
