using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using ServiceMarketplace.Application.DTOs;
using ServiceMarketplace.Domain.Enums;

namespace ServiceMarketplace.UI.Shared.Requests;

public sealed class MarketplaceSearchApiClient(HttpClient httpClient)
{
    public async Task<MarketplaceSearchResponse> SearchAsync(MarketplaceSearchRequest request, CancellationToken cancellationToken = default)
    {
        var query = new List<string>();

        if (!string.IsNullOrWhiteSpace(request.Query))
            query.Add($"q={Uri.EscapeDataString(request.Query)}");
        if (request.Type.HasValue)
            query.Add($"type={(int)request.Type.Value}");
        if (request.ServiceCategoryId.HasValue)
            query.Add($"serviceCategoryId={request.ServiceCategoryId.Value}");
        if (request.ProductCategoryId.HasValue)
            query.Add($"productCategoryId={request.ProductCategoryId.Value}");
        if (request.ZoneId.HasValue)
            query.Add($"zoneId={request.ZoneId.Value}");
        if (request.ProductCondition.HasValue)
            query.Add($"productCondition={(int)request.ProductCondition.Value}");
        if (request.MinPrice.HasValue)
            query.Add($"minPrice={request.MinPrice.Value.ToString(CultureInfo.InvariantCulture)}");
        if (request.MaxPrice.HasValue)
            query.Add($"maxPrice={request.MaxPrice.Value.ToString(CultureInfo.InvariantCulture)}");
        query.Add($"sort={(int)request.Sort}");
        query.Add($"take={request.Take}");

        var path = $"api/marketplace/search?{string.Join("&", query)}";
        using var response = await httpClient.GetAsync(path, cancellationToken);
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException(await TryReadErrorAsync(response) ?? "Failed to search marketplace.");

        return await response.Content.ReadFromJsonAsync<MarketplaceSearchResponse>(cancellationToken: cancellationToken)
            ?? new MarketplaceSearchResponse();
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
        }
        catch
        {
            return null;
        }

        return null;
    }
}
