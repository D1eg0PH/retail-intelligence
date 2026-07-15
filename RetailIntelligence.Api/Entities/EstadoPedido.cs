namespace RetailIntelligence.Api.Entities;

public class EstadoPedido
{
    public int IdEstado { get; set; }

    public string Nombre { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public ICollection<Pedido> Pedidos { get; set; }
        = new List<Pedido>();
}