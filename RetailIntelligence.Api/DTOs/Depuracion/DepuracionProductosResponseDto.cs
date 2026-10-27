namespace RetailIntelligence.Api.Dtos.Depuracion;

public class DepuracionProductosResponseDto
{
    public DateOnly FechaInicioAnalisis { get; set; }
    public DateOnly FechaFinAnalisis { get; set; }
    public int DiasPeriodo { get; set; }
    public int DiasObjetivo { get; set; }
    public int DiasCubiertos { get; set; }
    public bool CoberturaCompleta { get; set; }
    public FuenteInventarioDepuracionDto? FuenteInventario { get; set; }
    public List<PeriodoVentasDepuracionDto> PeriodosVentas { get; set; } = [];
    public List<ProductoDepuracionDto> Productos { get; set; } = [];
}
