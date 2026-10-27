using Microsoft.AspNetCore.Mvc;
using RetailIntelligence.Api.Interfaces;
using RetailIntelligence.Api.Dtos;

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
        try
        {
            await _pedidoService.GenerarPedidosAsync();
        }
        catch (InvalidOperationException exception)
        {
            return BadRequest(new { mensaje = exception.Message });
        }

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

    [HttpPost("{idPedido}/productos/{idProducto}")]
    public async Task<IActionResult>AgregarProducto(int idPedido, int idProducto)
    {
        await _pedidoService.AgregarProductoAsync(idPedido,idProducto);

        return Ok();
    }

    [HttpPost("quitar")]
    public async Task<IActionResult>QuitarProductos(
    [FromBody] List<int> detalles)
    {
        await _pedidoService
            .QuitarProductosAsync(
                detalles);

        return Ok();
    }


    [HttpGet("{id}/excel")]
    public async Task<IActionResult> ExportExcel(
    int id,
    [FromServices] IExcelExportService excelService)
    {
        var archivo = await excelService.ExportPedidoAsync(id);

        return File(
            archivo,
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            $"Pedido_{id}.xlsx");
    }

    [HttpPut("{id}/cantidades")]
    public async Task<IActionResult> ActualizarCantidades(int id,[FromBody] List<ActualizarCantidadesDto> cantidades)
    {
        await _pedidoService.ActualizarCantidadesAsync(id,cantidades);

        return NoContent();
    }
}

