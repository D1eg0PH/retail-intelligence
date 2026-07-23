using Microsoft.EntityFrameworkCore;
using RetailIntelligence.Api.Data;
using RetailIntelligence.Api.Interfaces;

namespace RetailIntelligence.Api.Services;

public class ProductoService : IProductoService
{
    private readonly AppDbContext _context;

    public ProductoService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<List<object>> GetAllAsync()
    {
        return await _context.Productos
            .Select(p => new
            {
                p.IdProducto,
                p.Codigo,
                p.Nombre,
                p.CostoActual,
                p.PrecioVentaActual,

                Proveedor = p.Proveedor.Nombre,
                Departamento = p.Departamento.Nombre
            })
            .Cast<object>()
            .ToListAsync();
    }
}