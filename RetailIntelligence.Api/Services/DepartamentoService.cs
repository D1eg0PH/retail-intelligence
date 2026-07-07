using Microsoft.EntityFrameworkCore;
using RetailIntelligence.Api.Data;
using RetailIntelligence.Api.DTOs;
using RetailIntelligence.Api.Interfaces;

namespace RetailIntelligence.Api.Services;

public class DepartamentoService : IDepartamentoService
{
    private readonly AppDbContext _context;

    public DepartamentoService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<List<DepartamentoDto>> GetAllAsync()
    {
        return await _context.Departamentos
            .OrderBy(d => d.Nombre)
            .Select(d => new DepartamentoDto
            {
                Id = d.IdDepartamento,
                Nombre = d.Nombre
            })
            .ToListAsync();
    }
}