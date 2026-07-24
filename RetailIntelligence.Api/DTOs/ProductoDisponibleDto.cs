namespace RetailIntelligence.Api.Dtos.Pedido;

public class ProductoDisponibleDto
{
    public int IdProducto { get; set; }

    public string Nombre { get; set; } = string.Empty;

    public decimal Existencia { get; set; }

    public decimal VendidoPeriodo { get; set; }

    public decimal CostoActual { get; set; }
}