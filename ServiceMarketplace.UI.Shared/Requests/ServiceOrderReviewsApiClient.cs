using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using ServiceMarketplace.Application.DTOs;
using ServiceMarketplace.UI.Shared.Auth;

namespace ServiceMarketplace.UI.Shared.Requests;

public sealed class ServiceOrderReviewsApiClient(HttpClient httpClient, ITokenStorage tokenStorage)
{
    private readonly HttpClient _httpClient = httpClient;
    private readonly ITokenStorage _tokenStorage = tokenStorage;

    public async Task<ServiceOrderReviewDto?> GetForOrderAsync(Guid orderId, CancellationToken cancellationToken = default)
    {
        await AttachBearerAsync();
        using var response = await _httpClient.GetAsync($"api/orders/{orderId}/review", cancellationToken);
        if (response.StatusCode == HttpStatusCode.NoContent)
            return null;

        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException(await TryReadErrorAsync(response) ?? "Failed to load review.");

        return await response.Content.ReadFromJsonAsync<ServiceOrderReviewDto>(cancellationToken: cancellationToken);
    }

    public async Task<List<ServiceOrderReviewDto>> GetForProviderAsync(string providerId, CancellationToken cancellationToken = default)
    {
        await AttachBearerAsync();
        using var response = await _httpClient.GetAsync($"api/providers/{providerId}/reviews", cancellationToken);
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException(await TryReadErrorAsync(response) ?? "Failed to load reviews.");

        return await response.Content.ReadFromJsonAsync<List<ServiceOrderReviewDto>>(cancellationToken: cancellationToken) ?? [];
    }

    public async Task<List<ServiceOrderReviewDto>> GetForAdminAsync(bool includeHidden = true, CancellationToken cancellationToken = default)
    {
        await AttachBearerAsync();
        using var response = await _httpClient.GetAsync($"api/admin/order-reviews?includeHidden={includeHidden}", cancellationToken);
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException(await TryReadErrorAsync(response) ?? "Failed to load reviews.");

        return await response.Content.ReadFromJsonAsync<List<ServiceOrderReviewDto>>(cancellationToken: cancellationToken) ?? [];
    }

    public async Task<ServiceOrderReviewDto> ModerateAsync(Guid reviewId, ModerateServiceOrderReviewDto dto, CancellationToken cancellationToken = default)
    {
        await AttachBearerAsync();
        using var response = await _httpClient.PostAsJsonAsync($"api/admin/order-reviews/{reviewId}/moderate", dto, cancellationToken);
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException(await TryReadErrorAsync(response) ?? "Failed to moderate review.");

        return await response.Content.ReadFromJsonAsync<ServiceOrderReviewDto>(cancellationToken: cancellationToken)
            ?? throw new InvalidOperationException("Empty response from server.");
    }

    public async Task<ServiceOrderReviewDto> CreateAsync(Guid orderId, CreateServiceOrderReviewDto dto, CancellationToken cancellationToken = default)
    {
        await AttachBearerAsync();
        using var response = await _httpClient.PostAsJsonAsync($"api/orders/{orderId}/review", dto, cancellationToken);
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException(await TryReadErrorAsync(response) ?? "Failed to create review.");

        return await response.Content.ReadFromJsonAsync<ServiceOrderReviewDto>(cancellationToken: cancellationToken)
            ?? throw new InvalidOperationException("Empty response from server.");
    }

    private async Task AttachBearerAsync()
    {
        var token = await _tokenStorage.GetTokenAsync();
        _httpClient.DefaultRequestHeaders.Authorization = string.IsNullOrWhiteSpace(token)
            ? null
            : new AuthenticationHeaderValue("Bearer", token);
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
