namespace RetailIntelligence.Api.Entities;

public class Proveedor
{
    public int IdProveedor { get; set; }

    public string Nombre { get; set; } = string.Empty;

    public bool Activo { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public ICollection<Producto> Productos { get; set; }
        = new List<Producto>();

}