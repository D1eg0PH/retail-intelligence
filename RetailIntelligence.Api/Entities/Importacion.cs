namespace RetailIntelligence.Api.Entities;

public class Importacion
{
    public int IdImportacion { get; set; }

    public string TipoArchivo { get; set; } = string.Empty;

    public string NombreArchivo { get; set; } = string.Empty;

    public DateOnly FechaInicioPeriodo { get; set; }

    public DateOnly FechaFinPeriodo { get; set; }

    public string HashArchivo { get; set; } = string.Empty;

    public int RegistrosImportados { get; set; }

    public string? Observaciones { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }
}