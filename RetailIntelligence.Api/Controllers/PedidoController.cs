
using Microsoft.AspNetCore.Mvc;
using RetailIntelligence.Api.Interfaces;
namespace RetailIntelligence.Api.Controllers;



[ApiController]
[Route("api/pedidos")]
public class PedidoController : ControllerBase
{
    private readonly IPedidoService _pedidoService;

    public PedidoController(
        IPedidoService pedidoService)
    {
        _pedidoService = pedidoService;
    }

    [HttpPost("generar")]
    public async Task<IActionResult> Generar()
    {
        await _pedidoService.GenerarPedidosAsync();

        return Ok("Pedidos generados.");
    }

    [HttpGet]
    public async Task<IActionResult> GetPedidos()
    {
        return Ok(
            await _pedidoService.GetPedidosAsync());
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetPedido(int id)
    {
        var pedido = await _pedidoService.GetPedidoAsync(id);

        if (pedido == null)
        {
            return NotFound(new
            {
                mensaje = $"No existe el pedido {id}"
            });
        }

        return Ok(pedido);
    }

    [HttpPut("{id}/estado")]
    public async Task<IActionResult> CambiarEstado(int id,int estado)
    {
        await _pedidoService.CambiarEstadoAsync(id,estado);
        return Ok("Estado actualizado.");
    }

    [HttpPut("detalle/{idDetalle}/cantidad")]
    public async Task<IActionResult> ActualizarCantidad(int idDetalle,[FromBody] decimal cantidad)
    {
        await _pedidoService.ActualizarCantidadAsync(idDetalle,cantidad);

        return Ok();
    }
}