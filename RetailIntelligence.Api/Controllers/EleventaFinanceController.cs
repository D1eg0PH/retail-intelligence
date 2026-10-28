using Microsoft.AspNetCore.Mvc;
using RetailIntelligence.Api.Integrations.Eleventa;
using RetailIntelligence.Contracts.Eleventa;

namespace RetailIntelligence.Api.Controllers;

[ApiController]
[Route("api/integrations/eleventa/v1/finance")]
[ServiceFilter(typeof(EleventaApiKeyFilter))]
public sealed class EleventaFinanceController(EleventaFinanceService service) : ControllerBase
{
    private Guid Source => (Guid)HttpContext.Items[EleventaApiKeyFilter.AuthenticatedSourceItem]!;

    private static bool ValidRange(DateOnly from, DateOnly to)
    {
        var today = DateOnly.FromDateTime(EleventaClosingSchedule.LocalTime(DateTimeOffset.UtcNow));
        return from >= new DateOnly(2000, 1, 1) && from <= today && from <= to &&
            to <= today.AddDays(31) && to.DayNumber - from.DayNumber <= 366;
    }

    [HttpGet("entries")]
    public async Task<ActionResult<FinanceEntryPage>> Entries([FromQuery] DateOnly from, [FromQuery] DateOnly to,
        [FromQuery] int skip = 0, [FromQuery] int take = 50, CancellationToken cancellationToken = default)
    {
        if (!ValidRange(from, to) || skip < 0 || take is < 1 or > 200)
            return BadRequest(new ProblemDetails { Title = "Revisa el período y la paginación." });
        return Ok(await service.ListAsync(Source, from, to, skip, take, cancellationToken));
    }

    [HttpPost("entries")]
    public async Task<ActionResult<FinanceEntryView>> Create([FromBody] FinanceEntryRequest request, CancellationToken cancellationToken)
    {
        try { return Ok(await service.CreateAsync(Source, request, DateTimeOffset.UtcNow, cancellationToken)); }
        catch (ArgumentException ex) { return BadRequest(new ProblemDetails { Title = "Movimiento inválido", Detail = ex.Message }); }
        catch (EleventaBatchConflictException ex) { return Conflict(new ProblemDetails { Title = "Movimiento ya registrado", Detail = ex.Message }); }
    }

    [HttpPost("entries/{id:guid}/void")]
    public async Task<ActionResult<FinanceEntryView>> Void(Guid id, [FromBody] FinanceVoidRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var entry = await service.VoidAsync(Source, id, request.Reason, DateTimeOffset.UtcNow, cancellationToken);
            return entry is null ? NotFound() : Ok(entry);
        }
        catch (ArgumentException ex) { return BadRequest(new ProblemDetails { Title = "Anulación inválida", Detail = ex.Message }); }
    }

    [HttpGet("summary")]
    public async Task<ActionResult<FinanceSummary>> Summary([FromQuery] DateOnly from, [FromQuery] DateOnly to, CancellationToken cancellationToken)
    {
        if (!ValidRange(from, to)) return BadRequest(new ProblemDetails { Title = "El período debe comenzar hoy o antes y abarcar como máximo 366 días." });
        return Ok(await service.SummaryAsync(Source, from, to, DateTimeOffset.UtcNow, cancellationToken));
    }
}
