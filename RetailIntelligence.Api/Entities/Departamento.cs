namespace RetailIntelligence.Api.Entities;

public class Departamento
{
    public int IdDepartamento { get; set; }

    public string Nombre { get; set; } = string.Empty;

    public bool Activo { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }
}