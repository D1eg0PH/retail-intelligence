using System.ComponentModel.DataAnnotations;

namespace RetailIntelligence.Api.Dtos;

public class ConfiguracionDto
{
    public short SemanasPromedio { get; set; }

    public short DiasInventarioObjetivo { get; set; }

    public short DiasProductoMuerto { get; set; }

    public decimal PorcentajeColchon { get; set; }

    [Range(typeof(decimal), "0", "100")]
    public decimal PorcentajeUmbralReposicion { get; set; } = 75m;
}
