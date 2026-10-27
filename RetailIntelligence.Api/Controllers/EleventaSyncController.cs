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

    [HttpGet("inventory/history")]
    public async Task<ActionResult<EleventaInventoryHistoryResponse>> GetInventoryHistory(
        [FromQuery] int skip = 0, [FromQuery] int take = 30, CancellationToken cancellationToken = default)
    {
        if (skip < 0 || take is < 1 or > 100)
            return BadRequest(new ProblemDetails { Status = 400, Title = "Paginación inválida", Detail = "skip debe ser no negativo y take entre 1 y 100." });
        var sourceId = (Guid)HttpContext.Items[EleventaApiKeyFilter.AuthenticatedSourceItem]!;
        return Ok(await syncService.GetInventoryHistoryAsync(sourceId, skip, take, cancellationToken));
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

    [HttpGet("dashboard")]
    public async Task<ActionResult<EleventaDashboardResponse>> GetDashboard(
        [FromQuery] int skip = 0, [FromQuery] int take = 100, [FromQuery] string? search = null,
        CancellationToken cancellationToken = default)
    {
        if (skip < 0 || take is < 1 or > 500)
            return BadRequest(new ProblemDetails { Status = 400, Title = "Paginación inválida", Detail = "skip debe ser no negativo y take entre 1 y 500." });
        var sourceId = (Guid)HttpContext.Items[EleventaApiKeyFilter.AuthenticatedSourceItem]!;
        return Ok(await syncService.GetDashboardAsync(sourceId, skip, take, search, cancellationToken));
    }

    [HttpGet("products/cleanup")]
    public async Task<ActionResult<EleventaProductCleanupResponse>> GetProductCleanup(
        [FromQuery] int skip = 0, [FromQuery] int take = 100, [FromQuery] int inactiveDays = 60,
        [FromQuery] string? search = null, [FromQuery] string? classification = null,
        [FromQuery] int? departmentId = null,
        CancellationToken cancellationToken = default)
    {
        if (skip < 0 || take is < 1 or > 500 || inactiveDays is < 30 or > 365)
            return BadRequest(new ProblemDetails { Status = 400, Title = "Parámetros inválidos", Detail = "skip debe ser no negativo, take entre 1 y 500 e inactiveDays entre 30 y 365." });
        var validClassifications = new[] { "all", "candidate", "recent-movement", "no-sales-observed", "no-inventory", "deleted", "missing-inventory" };
        if (classification is not null && !validClassifications.Contains(classification, StringComparer.Ordinal))
            return BadRequest(new ProblemDetails { Status = 400, Title = "Clasificación inválida", Detail = "La clasificación solicitada no existe." });
        var sourceId = (Guid)HttpContext.Items[EleventaApiKeyFilter.AuthenticatedSourceItem]!;
        return Ok(await syncService.GetProductCleanupAsync(sourceId, skip, take, inactiveDays, search, classification, departmentId, cancellationToken));
    }

    [HttpGet("order-suggestions")]
    public async Task<ActionResult<EleventaOrderSuggestionsResponse>> GetOrderSuggestions(CancellationToken cancellationToken)
    {
        var sourceId = (Guid)HttpContext.Items[EleventaApiKeyFilter.AuthenticatedSourceItem]!;
        try
        {
            return Ok(await syncService.GetOrderSuggestionsAsync(sourceId, cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new ProblemDetails { Status = 409, Title = "Configuración no disponible", Detail = ex.Message });
        }
    }
}
