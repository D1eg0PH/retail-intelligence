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
}