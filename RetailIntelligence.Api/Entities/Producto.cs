namespace RetailIntelligence.Api.Entities;

public class Producto
{
    public int IdProducto { get; set; }

    public int IdProveedor { get; set; }

    public int IdDepartamento { get; set; }

    public string Codigo { get; set; } = string.Empty;

    public string? CodigoBarras { get; set; }

    public string Nombre { get; set; } = string.Empty;

    public bool Activo { get; set; }

    public bool EsEsencial { get; set; }

    public decimal StockMinimo { get; set; }

    public decimal StockMaximo { get; set; }

    public string UnidadMedida { get; set; } = string.Empty;

    public decimal CostoActual { get; set; }

    public decimal PrecioVentaActual { get; set; }

    public bool Descontinuado { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public Departamento Departamento { get; set; } = null!;

    public Proveedor Proveedor { get; set; } = null!;
}