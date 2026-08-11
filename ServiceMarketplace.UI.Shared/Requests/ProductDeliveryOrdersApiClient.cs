using System.Net.Http.Json;
using System.Text.Json;
using ServiceMarketplace.Application.DTOs;

namespace ServiceMarketplace.UI.Shared.Requests;

public sealed class ProductDeliveryOrdersApiClient(HttpClient httpClient)
{
    public async Task<ProductDeliveryOrderDto> CreateAsync(CreateProductDeliveryOrderDto dto, CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.PostAsJsonAsync("api/product-delivery-orders", dto, cancellationToken);
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException(await TryReadErrorAsync(response) ?? "Failed to create product delivery order.");

        return await response.Content.ReadFromJsonAsync<ProductDeliveryOrderDto>(cancellationToken: cancellationToken)
            ?? throw new InvalidOperationException("Empty product delivery order response.");
    }

    public async Task<List<ProductDeliveryOrderDto>> GetMineAsBuyerAsync(CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.GetAsync("api/product-delivery-orders/mine/buyer", cancellationToken);
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException(await TryReadErrorAsync(response) ?? "Failed to load product delivery orders.");

        return await response.Content.ReadFromJsonAsync<List<ProductDeliveryOrderDto>>(cancellationToken: cancellationToken) ?? [];
    }

    public async Task<List<ProductDeliveryOrderDto>> GetMineAsSellerAsync(CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.GetAsync("api/product-delivery-orders/mine/seller", cancellationToken);
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException(await TryReadErrorAsync(response) ?? "Failed to load seller product delivery orders.");

        return await response.Content.ReadFromJsonAsync<List<ProductDeliveryOrderDto>>(cancellationToken: cancellationToken) ?? [];
    }

    public async Task<ProductDeliveryOrderDto> UpdateSellerStatusAsync(Guid orderId, UpdateProductDeliveryStatusDto dto, CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.PostAsJsonAsync($"api/product-delivery-orders/{orderId}/seller-status", dto, cancellationToken);
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException(await TryReadErrorAsync(response) ?? "Failed to update product delivery order.");

        return await response.Content.ReadFromJsonAsync<ProductDeliveryOrderDto>(cancellationToken: cancellationToken)
            ?? throw new InvalidOperationException("Empty product delivery order response.");
    }

    public async Task<ProductDeliveryOrderDto> CancelAsBuyerAsync(Guid orderId, string? cancellationReason = null, CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.PostAsJsonAsync(
            $"api/product-delivery-orders/{orderId}/buyer-cancel",
            new UpdateProductDeliveryStatusDto { CancellationReason = cancellationReason },
            cancellationToken);

        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException(await TryReadErrorAsync(response) ?? "Failed to cancel product delivery order.");

        return await response.Content.ReadFromJsonAsync<ProductDeliveryOrderDto>(cancellationToken: cancellationToken)
            ?? throw new InvalidOperationException("Empty product delivery order response.");
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
