namespace RetailIntelligence.Api.Interfaces;

public interface IImportService
{
    Task ImportInventoryAsync(IFormFile file);

    Task ImportSalesAsync(
      IFormFile file,
      DateOnly fechaInicioPeriodo,
      DateOnly fechaFinPeriodo);
}