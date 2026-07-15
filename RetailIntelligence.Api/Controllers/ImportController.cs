using Microsoft.AspNetCore.Mvc;
using RetailIntelligence.Api.Interfaces;

namespace RetailIntelligence.Api.Controllers;

[ApiController]
[Route("api/import")]
public class ImportController : ControllerBase
{
    private readonly IImportService _importService;

    public ImportController(IImportService importService)
    {
        _importService = importService;
    }

    [HttpPost("inventory")]
    public async Task<IActionResult> ImportInventory(IFormFile file)
    {
        if (file == null || file.Length == 0)
            return BadRequest("Debe seleccionar un archivo.");

        await _importService.ImportInventoryAsync(file);

        return Ok("Archivo recibido correctamente.");
    }

    [HttpPost("sales")]
    public async Task<IActionResult> ImportSales(IFormFile file,DateOnly fechaInicioPeriodo,DateOnly fechaFinPeriodo)
    {
        if (fechaInicioPeriodo == default ||fechaFinPeriodo == default)
        {
            return BadRequest(
                "Debe especificar el periodo de ventas.");
        }
        await _importService.ImportSalesAsync(file,fechaInicioPeriodo,fechaFinPeriodo);

        return Ok();
    }
}