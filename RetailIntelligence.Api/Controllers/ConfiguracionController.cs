using Microsoft.AspNetCore.Mvc;
using RetailIntelligence.Api.Interfaces;
using RetailIntelligence.Api.Dtos;

namespace RetailIntelligence.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ConfiguracionController : ControllerBase
{
    private readonly IConfiguracionService _configuracionService;

    public ConfiguracionController(IConfiguracionService configuracionService)
    {
        _configuracionService = configuracionService;
    }

    [HttpGet]
    public async Task<ActionResult<ConfiguracionDto>> Get()
    {
        var configuracion = await _configuracionService.GetAsync();

        return Ok(configuracion);
    }

    [HttpPut]
    public async Task<IActionResult> Update(
        [FromBody] ConfiguracionDto configuracion)
    {
        await _configuracionService.UpdateAsync(configuracion);

        return NoContent();
    }
}