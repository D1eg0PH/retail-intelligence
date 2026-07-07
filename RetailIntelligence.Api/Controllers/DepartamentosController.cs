using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RetailIntelligence.Api.Data;
using RetailIntelligence.Api.DTOs;

namespace RetailIntelligence.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class DepartamentosController : ControllerBase
{
    private readonly AppDbContext _context;

    public DepartamentosController(AppDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<IActionResult> Get()
    {
        var departamentos = await _context.Departamentos
            .OrderBy(d => d.Nombre)
            .Select(d => new DepartamentoDto
            {
                Id = d.IdDepartamento,
                Nombre = d.Nombre
            })
            .ToListAsync();

        return Ok(departamentos);
    }
}