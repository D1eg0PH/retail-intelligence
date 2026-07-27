namespace RetailIntelligence.Api.Dtos.Pedido;

public class PedidoDto
{
    public int IdPedido { get; set; }

    public string Proveedor { get; set; } = string.Empty;

    public string Estado { get; set; } = string.Empty;

    public DateOnly FechaPedido { get; set; }

    public int Productos { get; set; }

    public decimal MontoEstimado { get; set; }
}