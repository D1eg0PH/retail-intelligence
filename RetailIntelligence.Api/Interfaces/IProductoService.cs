
using RetailIntelligence.Api.Dtos.Producto;

namespace RetailIntelligence.Api.Interfaces;

public interface IProductoService
{
    Task<List<ProductoDto>> GetAllAsync();
}