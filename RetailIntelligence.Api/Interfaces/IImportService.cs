using RetailIntelligence.Api.DTOs;

namespace RetailIntelligence.Api.Interfaces;

public interface IImportService
{
    Task ImportInventoryAsync(IFormFile file);

    Task ImportSalesAsync(ImportSalesRequest request);
}