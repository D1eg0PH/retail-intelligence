namespace RetailIntelligence.Api.Dtos.Depuracion;

public class ProductoDepuracionDto
{
    public int IdProducto { get; set; }

    public string Codigo { get; set; } = string.Empty;

    public string Nombre { get; set; } = string.Empty;

    public string Proveedor { get; set; } = string.Empty;

    public decimal Existencia { get; set; }

    public decimal CostoActual { get; set; }

    public decimal ValorInventario { get; set; }

    public decimal VendidoPeriodo { get; set; }

    public int DiasPeriodo { get; set; }

    public string Clasificacion { get; set; } = string.Empty;
}