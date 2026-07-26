using Microsoft.AspNetCore.Http;

namespace RetailIntelligence.Api.DTOs;

public class ImportSalesRequest
{
    public required IFormFile File { get; set; }

    public int? DiasPeriodo { get; set; }

    public DateOnly? FechaInicio { get; set; }

    public DateOnly? FechaFin { get; set; }
}