using System.Net.Http.Json;
using ServiceMarketplace.Application.DTOs;

namespace ServiceMarketplace.UI.Shared.Requests;

public sealed class ServiceCatalogApiClient(HttpClient httpClient)
{
    public async Task<List<ServiceCategoryDto>> GetCategoriesAsync(CancellationToken cancellationToken = default)
    {
        return await httpClient.GetFromJsonAsync<List<ServiceCategoryDto>>(
            "api/service-catalog/categories",
            cancellationToken) ?? [];
    }

    public async Task<List<ServiceZoneDto>> GetZonesAsync(CancellationToken cancellationToken = default)
    {
        return await httpClient.GetFromJsonAsync<List<ServiceZoneDto>>(
            "api/service-catalog/zones",
            cancellationToken) ?? [];
    }
}
