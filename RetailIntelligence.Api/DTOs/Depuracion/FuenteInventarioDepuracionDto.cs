namespace RetailIntelligence.Api.Dtos.Depuracion;

public class FuenteInventarioDepuracionDto
{
    public string NombreArchivo { get; set; } = string.Empty;
    public DateOnly FechaInventario { get; set; }
    public int RegistrosImportados { get; set; }
    public DateTime ImportadoEl { get; set; }
}
