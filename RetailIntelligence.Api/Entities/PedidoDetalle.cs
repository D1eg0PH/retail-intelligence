namespace RetailIntelligence.Api.Entities;

public class PedidoDetalle
{
    public int IdDetalle { get; set; }

    public int IdPedido { get; set; }
    public Pedido Pedido { get; set; } = null!;

    public int IdProducto { get; set; }
    public Producto Producto { get; set; } = null!;

    public decimal CantidadSugerida { get; set; }

    public decimal? CantidadPedida { get; set; }

    public decimal CostoUnitario { get; set; }

    public string? MotivoAjuste { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }
}