using Microsoft.AspNetCore.Mvc;
using RetailIntelligence.Api.Interfaces;

namespace RetailIntelligence.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class DepartamentosController : ControllerBase
{
    private readonly IDepartamentoService _departamentoService;

    public DepartamentosController(IDepartamentoService departamentoService)
    {
        _departamentoService = departamentoService;
    }

    [HttpGet]
    public async Task<IActionResult> Get()
    {
        return Ok(await _departamentoService.GetAllAsync());
    }
}