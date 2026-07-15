public class ProductoDto
{
    public int Id { get; set; }

    public string Codigo { get; set; } = string.Empty;

    public string Nombre { get; set; } = string.Empty;

    public string Departamento { get; set; } = string.Empty;

    public decimal PrecioVenta { get; set; }

    public bool Activo { get; set; }
}