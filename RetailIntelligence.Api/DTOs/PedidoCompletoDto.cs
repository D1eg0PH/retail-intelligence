namespace RetailIntelligence.Api.Dtos.Pedido;

public class PedidoCompletoDto
{
    public int IdPedido { get; set; }

    public string Proveedor { get; set; } = string.Empty;

    public string Estado { get; set; } = string.Empty;

    public DateOnly FechaPedido { get; set; }

    public List<PedidoDetalleDto> Detalles { get; set; } = [];

    public List<ProductoDisponibleDto> ProductosDisponibles { get; set; } = [];

    public decimal MontoTotal { get; set; }
}