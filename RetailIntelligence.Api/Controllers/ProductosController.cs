using Microsoft.AspNetCore.Mvc;
using RetailIntelligence.Api.Interfaces;

namespace RetailIntelligence.Api.Controllers;

[ApiController]
[Route("api/productos")]
public class ProductosController : ControllerBase
{
    private readonly IProductoService _productoService;

    public ProductosController(IProductoService productoService)
    {
        _productoService = productoService;
    }

    [HttpGet]
    public async Task<IActionResult> GetProductos()
    {
        var productos = await _productoService.GetAllAsync();

        return Ok(productos);
    }
}