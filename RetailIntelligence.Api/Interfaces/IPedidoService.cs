using RetailIntelligence.Api.Dtos;

namespace RetailIntelligence.Api.Interfaces;

public interface IPedidoService
{
    Task GenerarPedidosAsync();

    Task<List<object>> GetPedidosAsync();

    Task<object?> GetPedidoAsync(int id);

    Task CambiarEstadoAsync(
        int idPedido,
        int idEstado);

    Task ActualizarCantidadAsync(
        int idDetalle,
        decimal cantidad);

    Task ActualizarCantidadesAsync(
        int idPedido,
        List<ActualizarCantidadesDto> cantidades);

    Task AgregarProductoAsync(
        int idPedido,
        int idProducto);

    Task QuitarProductosAsync(
        List<int> detalles);
}