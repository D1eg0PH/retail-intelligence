namespace RetailIntelligence.Api.Entities;

public class ConfiguracionSistema
{
    public short IdConfiguracion { get; set; }

    public short SemanasPromedio { get; set; }

    public short DiasInventarioObjetivo { get; set; }

    public short DiasProductoMuerto { get; set; }

    public decimal PorcentajeColchon { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }
}