namespace RetailIntelligence.Api.Entities;

public class Pedido
{
    public int IdPedido { get; set; }

    public int IdProveedor { get; set; }
    public Proveedor Proveedor { get; set; } = null!;

    public int IdEstado { get; set; }
    public EstadoPedido Estado { get; set; } = null!;

    public DateOnly FechaPedido { get; set; }

    public DateOnly? FechaEntrega { get; set; }

    public string? Observaciones { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public ICollection<PedidoDetalle> PedidoDetalles { get; set; }
        = new List<PedidoDetalle>();
}