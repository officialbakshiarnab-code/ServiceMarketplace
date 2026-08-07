using System.Net.Http.Json;
using ServiceMarketplace.Application.DTOs;

namespace ServiceMarketplace.UI.Shared.Requests;

public sealed class ProductCatalogApiClient(HttpClient httpClient)
{
    public async Task<List<ProductCategoryDto>> GetCategoriesAsync(CancellationToken cancellationToken = default)
    {
        return await httpClient.GetFromJsonAsync<List<ProductCategoryDto>>(
            "api/product-catalog/categories",
            cancellationToken) ?? [];
    }
}
