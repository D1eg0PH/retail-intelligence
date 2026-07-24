namespace RetailIntelligence.Api.Dtos.Producto;

public class ProductoDto
{
    public int IdProducto { get; set; }

    public string Codigo { get; set; } = string.Empty;

    public string Nombre { get; set; } = string.Empty;

    public decimal CostoActual { get; set; }

    public decimal PrecioVentaActual { get; set; }

    public string Proveedor { get; set; } = string.Empty;

    public string Departamento { get; set; } = string.Empty;
}