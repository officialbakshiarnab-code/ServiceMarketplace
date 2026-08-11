using System.Net.Http.Json;
using System.Text.Json;
using ServiceMarketplace.Application.DTOs;
using ServiceMarketplace.Domain.Enums;

namespace ServiceMarketplace.UI.Shared.Requests;

public sealed class ProductListingsApiClient(HttpClient httpClient)
{
    public async Task<List<ProductListingDto>> GetActiveAsync(Guid? categoryId = null, Guid? zoneId = null, ProductCondition? condition = null, CancellationToken cancellationToken = default)
    {
        var query = new List<string>();
        if (categoryId.HasValue)
            query.Add($"categoryId={categoryId.Value}");
        if (zoneId.HasValue)
            query.Add($"zoneId={zoneId.Value}");
        if (condition.HasValue)
            query.Add($"condition={(int)condition.Value}");

        var path = query.Count == 0 ? "api/products" : $"api/products?{string.Join("&", query)}";
        using var response = await httpClient.GetAsync(path, cancellationToken);
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException(await TryReadErrorAsync(response) ?? "Failed to load products.");

        return await response.Content.ReadFromJsonAsync<List<ProductListingDto>>(cancellationToken: cancellationToken) ?? [];
    }

    public async Task<List<ProductListingDto>> GetMineAsync(CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.GetAsync("api/products/mine", cancellationToken);
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException(await TryReadErrorAsync(response) ?? "Failed to load your products.");

        return await response.Content.ReadFromJsonAsync<List<ProductListingDto>>(cancellationToken: cancellationToken) ?? [];
    }

    public async Task<ProductListingDto> CreateAsync(UpsertProductListingDto dto, CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.PostAsJsonAsync("api/products", dto, cancellationToken);
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException(await TryReadErrorAsync(response) ?? "Failed to create product.");

        return await response.Content.ReadFromJsonAsync<ProductListingDto>(cancellationToken: cancellationToken)
            ?? throw new InvalidOperationException("Empty product response.");
    }

    public async Task<ProductListingDto> UpdateAsync(Guid listingId, UpsertProductListingDto dto, CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.PutAsJsonAsync($"api/products/{listingId}", dto, cancellationToken);
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException(await TryReadErrorAsync(response) ?? "Failed to update product.");

        return await response.Content.ReadFromJsonAsync<ProductListingDto>(cancellationToken: cancellationToken)
            ?? throw new InvalidOperationException("Empty product response.");
    }

    private static async Task<string?> TryReadErrorAsync(HttpResponseMessage response)
    {
        try
        {
            var text = await response.Content.ReadAsStringAsync();
            if (string.IsNullOrWhiteSpace(text))
                return null;

            using var doc = JsonDocument.Parse(text);
            if (doc.RootElement.TryGetProperty("message", out var msg) && msg.ValueKind == JsonValueKind.String)
                return msg.GetString();
            if (doc.RootElement.TryGetProperty("error", out var err) && err.ValueKind == JsonValueKind.String)
                return err.GetString();
            if (doc.RootElement.TryGetProperty("title", out var title) && title.ValueKind == JsonValueKind.String)
                return title.GetString();
        }
        catch
        {
            return null;
        }

        return null;
    }
}
