namespace RetailIntelligence.Api.Interfaces;

public interface IExcelExportService
{
    Task<byte[]> ExportPedidoAsync(int idPedido);
}