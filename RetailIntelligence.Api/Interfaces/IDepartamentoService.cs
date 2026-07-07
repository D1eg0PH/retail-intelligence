using RetailIntelligence.Api.DTOs;

namespace RetailIntelligence.Api.Interfaces;

public interface IDepartamentoService
{
    Task<List<DepartamentoDto>> GetAllAsync();
}