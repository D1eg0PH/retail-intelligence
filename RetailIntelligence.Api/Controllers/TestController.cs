
using Microsoft.AspNetCore.Mvc;

namespace RetailIntelligence.Api.Controllers;

[ApiController]
[Route("api/test")]
public class TestController : ControllerBase
{
    [HttpGet]
    public IActionResult Get()
    {
        return Ok("El controlador funciona");
    }
}