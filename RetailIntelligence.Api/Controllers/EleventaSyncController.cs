using Microsoft.AspNetCore.Mvc;
using RetailIntelligence.Api.Integrations.Eleventa;
using RetailIntelligence.Contracts.Eleventa;

namespace RetailIntelligence.Api.Controllers;

[ApiController]
[Route("api/integrations/eleventa/v1")]
[ServiceFilter(typeof(EleventaApiKeyFilter))]
public sealed class EleventaSyncController(EleventaSyncService syncService) : ControllerBase
{
    [HttpPost("batches")]
    [RequestSizeLimit(10_485_760)]
    public async Task<ActionResult<EleventaBatchResponse>> AcceptBatch(
        [FromBody] EleventaBatchRequest request, CancellationToken cancellationToken)
    {
        var sourceId = (Guid)HttpContext.Items[EleventaApiKeyFilter.AuthenticatedSourceItem]!;
        try
        {
            return Ok(await syncService.AcceptAsync(request, sourceId, cancellationToken));
        }
        catch (EleventaBatchConflictException ex)
        {
            return Conflict(new ProblemDetails { Status = 409, Title = "Conflicto de sincronización", Detail = ex.Message });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new ProblemDetails { Status = 400, Title = "Lote inválido", Detail = ex.Message });
        }
    }

    [HttpGet("status")]
    public async Task<ActionResult<EleventaSyncStatusResponse>> GetStatus(CancellationToken cancellationToken)
    {
        var sourceId = (Guid)HttpContext.Items[EleventaApiKeyFilter.AuthenticatedSourceItem]!;
        return Ok(await syncService.GetStatusAsync(sourceId, cancellationToken));
    }

    [HttpGet("catalog/products")]
    public async Task<ActionResult<EleventaProductPageResponse>> GetProducts(
        [FromQuery] int skip = 0, [FromQuery] int take = 100, CancellationToken cancellationToken = default)
    {
        if (skip < 0 || take is < 1 or > 500)
            return BadRequest(new ProblemDetails { Status = 400, Title = "Paginación inválida", Detail = "skip debe ser no negativo y take entre 1 y 500." });
        var sourceId = (Guid)HttpContext.Items[EleventaApiKeyFilter.AuthenticatedSourceItem]!;
        return Ok(await syncService.GetProductsAsync(sourceId, skip, take, cancellationToken));
    }
}
