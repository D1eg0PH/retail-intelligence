namespace RetailIntelligence.Api.Entities;


public class Inventario
{
    public int IdInventario { get; set; }

    public int IdProducto { get; set; }
    public Producto Producto { get; set; } = null!;

    public int IdImportacion { get; set; }
    public Importacion Importacion { get; set; } = null!;

    public decimal Existencia { get; set; }

    public decimal PrecioCosto { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }
}
