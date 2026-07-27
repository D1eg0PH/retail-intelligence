using RetailIntelligence.Api.Dtos;

namespace RetailIntelligence.Api.Interfaces;

public interface IConfiguracionService
{
    Task<ConfiguracionDto> GetAsync();

    Task UpdateAsync(ConfiguracionDto configuracion);
}