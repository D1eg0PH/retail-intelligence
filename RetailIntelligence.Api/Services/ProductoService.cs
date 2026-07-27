using Microsoft.EntityFrameworkCore;
using RetailIntelligence.Api.Data;
using RetailIntelligence.Api.Interfaces;
using RetailIntelligence.Api.Dtos.Producto;

namespace RetailIntelligence.Api.Services;

public class ProductoService : IProductoService
{
    private readonly AppDbContext _context;

    public ProductoService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<List<ProductoDto>> GetAllAsync()
    {
        return await _context.Productos
            .Select(p => new ProductoDto
            {
                IdProducto = p.IdProducto,
                Codigo = p.Codigo,
                Nombre = p.Nombre,
                CostoActual = p.CostoActual,
                PrecioVentaActual = p.PrecioVentaActual,
                Proveedor = p.Proveedor.Nombre,
                Departamento = p.Departamento.Nombre
            })
            .ToListAsync();
    }
}