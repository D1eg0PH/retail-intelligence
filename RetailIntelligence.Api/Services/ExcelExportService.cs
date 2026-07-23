using ClosedXML.Excel;
using RetailIntelligence.Api.Data;
using RetailIntelligence.Api.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace RetailIntelligence.Api.Services;

public class ExcelExportService : IExcelExportService
{
    private readonly AppDbContext _context;

    public ExcelExportService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<byte[]> ExportPedidoAsync(int idPedido)
    {

        var pedido = await _context.Pedidos
            .Include(p => p.Proveedor)
            .Include(p => p.PedidoDetalles)
                .ThenInclude(d => d.Producto)
            .FirstOrDefaultAsync(p => p.IdPedido == idPedido);

        if (pedido == null)
        {
            throw new KeyNotFoundException("Pedido no encontrado.");
        }


        using var workbook = new XLWorkbook();

        var ws = workbook.Worksheets.Add("Pedido");

        ws.Cell("A1").Value = "PEDIDO";

        ws.Range("A1:D1").Merge();
        ws.Cell("A1").Style.Font.Bold = true;
        ws.Cell("A1").Style.Font.FontSize = 18;
        ws.Cell("A1").Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

        ws.Cell("A2").Value = pedido.Proveedor.Nombre;

        ws.Range("A2:D2").Merge();
        ws.Cell("A2").Style.Font.Bold = true;
        ws.Cell("A2").Style.Font.FontSize = 14;
        ws.Cell("A2").Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

        ws.Cell("A4").Value = "Cant.";
        ws.Cell("B4").Value = "Descripción";
        ws.Cell("C4").Value = "Costo";
        ws.Cell("D4").Value = "Importe";

        ws.Range("A4:D4").Style.Font.Bold = true;

        int fila = 5;

        foreach (var detalle in pedido.PedidoDetalles)
        {
            var cantidad = detalle.CantidadPedida ?? detalle.CantidadSugerida;

            ws.Cell(fila, 1).Value = cantidad;
            ws.Cell(fila, 2).Value = detalle.Producto.Nombre;
            ws.Cell(fila, 3).Value = detalle.CostoUnitario;
            ws.Cell(fila, 4).Value = cantidad * detalle.CostoUnitario;

            fila++;
        }

        ws.Cell(fila + 1, 1).Value = "Productos distintos:";
        ws.Cell(fila + 1, 2).Value = pedido.PedidoDetalles.Count;

        ws.Cell(fila + 2, 1).Value = "Unidades a solicitar:";
        ws.Cell(fila + 2, 2).Value = pedido.PedidoDetalles.Sum(x => x.CantidadPedida ?? x.CantidadSugerida);

        ws.Cell(fila + 4, 3).Value = "TOTAL:";
        ws.Cell(fila + 4, 4).Value =pedido.PedidoDetalles.Sum(x => (x.CantidadPedida ?? x.CantidadSugerida) * x.CostoUnitario);

        ws.Column(3).Style.NumberFormat.Format = "$#,##0.00";
        ws.Column(4).Style.NumberFormat.Format = "$#,##0.00";

        ws.Columns().AdjustToContents();

        using var stream = new MemoryStream();

        workbook.SaveAs(stream);

        return stream.ToArray();
    }
}