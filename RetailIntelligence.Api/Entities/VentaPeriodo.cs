namespace RetailIntelligence.Api.Entities;

public class VentaPeriodo
{
    public int IdVenta { get; set; }

    public int IdProducto { get; set; }
    public Producto Producto { get; set; } = null!;

    public int IdImportacion { get; set; }
    public Importacion Importacion { get; set; } = null!;

    public decimal CantidadVendida { get; set; }

    public decimal PrecioUsado { get; set; }

    public decimal PrecioCosto { get; set; }

    public decimal Importe { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }
}