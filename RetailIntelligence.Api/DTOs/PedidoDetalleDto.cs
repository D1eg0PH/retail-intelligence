namespace RetailIntelligence.Api.Dtos.Pedido;

public class PedidoDetalleDto
{
    public int IdDetalle { get; set; }

    public string Producto { get; set; } = string.Empty;

    public decimal Existencia { get; set; }

    public decimal VendidoPeriodo { get; set; }

    public decimal CantidadSugerida { get; set; }

    public decimal? CantidadPedida { get; set; }

    public decimal CostoUnitario { get; set; }

    public decimal Subtotal { get; set; }
}