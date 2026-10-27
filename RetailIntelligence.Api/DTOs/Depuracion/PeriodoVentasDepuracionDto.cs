namespace RetailIntelligence.Api.Dtos.Depuracion;

public class PeriodoVentasDepuracionDto
{
    public string NombreArchivo { get; set; } = string.Empty;
    public DateOnly FechaInicio { get; set; }
    public DateOnly FechaFin { get; set; }
    public int RegistrosImportados { get; set; }
    public DateTime ImportadoEl { get; set; }
}
