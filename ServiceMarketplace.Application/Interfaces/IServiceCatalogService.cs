using ServiceMarketplace.Application.DTOs;

namespace ServiceMarketplace.Application.Interfaces;

public interface IServiceCatalogService
{
    Task<IReadOnlyList<ServiceCategoryDto>> GetCategoriesAsync(bool activeOnly = true);
    Task<IReadOnlyList<ServiceZoneDto>> GetZonesAsync(bool activeOnly = true);
}
