using RetailIntelligence.Api.Entities;

namespace RetailIntelligence.Api.Interfaces;

public interface IProductoService
{
    Task<List<object>> GetAllAsync();
}