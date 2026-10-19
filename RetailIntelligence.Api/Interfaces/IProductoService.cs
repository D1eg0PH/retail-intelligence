using RetailIntelligence.Api.Dtos.Producto;
using RetailIntelligence.Api.Dtos.Depuracion;

namespace RetailIntelligence.Api.Interfaces;

public interface IProductoService
{
    Task<List<ProductoDto>> GetAllAsync();

    Task<DepuracionProductosResponseDto> GetDepuracionAsync();
}
