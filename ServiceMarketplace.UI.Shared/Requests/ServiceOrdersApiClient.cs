using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using ServiceMarketplace.Application.DTOs;
using ServiceMarketplace.UI.Shared.Auth;

namespace ServiceMarketplace.UI.Shared.Requests;

public sealed class ServiceOrdersApiClient(HttpClient httpClient, ITokenStorage tokenStorage)
{
    private readonly HttpClient _httpClient = httpClient;
    private readonly ITokenStorage _tokenStorage = tokenStorage;

    public async Task<List<ServiceOrderDto>> GetCustomerOrdersAsync(CancellationToken cancellationToken = default)
    {
        await AttachBearerAsync();
        using var response = await _httpClient.GetAsync("api/orders/customer", cancellationToken);
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException(await TryReadErrorAsync(response) ?? "Failed to load service orders.");

        return await response.Content.ReadFromJsonAsync<List<ServiceOrderDto>>(cancellationToken: cancellationToken) ?? [];
    }

    public async Task<List<ServiceOrderDto>> GetProviderOrdersAsync(CancellationToken cancellationToken = default)
    {
        await AttachBearerAsync();
        using var response = await _httpClient.GetAsync("api/orders/provider", cancellationToken);
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException(await TryReadErrorAsync(response) ?? "Failed to load service orders.");

        return await response.Content.ReadFromJsonAsync<List<ServiceOrderDto>>(cancellationToken: cancellationToken) ?? [];
    }

    public async Task<ServiceOrderDto> StartAsync(Guid orderId, CancellationToken cancellationToken = default)
    {
        return await PostActionAsync($"api/orders/{orderId}/start", null, cancellationToken);
    }

    public async Task<ServiceOrderDto> ProviderCompleteAsync(Guid orderId, CancellationToken cancellationToken = default)
    {
        return await PostActionAsync($"api/orders/{orderId}/provider-complete", null, cancellationToken);
    }

    public async Task<ServiceOrderDto> ConfirmCompleteAsync(Guid orderId, CancellationToken cancellationToken = default)
    {
        return await PostActionAsync($"api/orders/{orderId}/confirm-complete", null, cancellationToken);
    }

    public async Task<ServiceOrderDto> CancelAsync(Guid orderId, string reason, CancellationToken cancellationToken = default)
    {
        return await PostActionAsync($"api/orders/{orderId}/cancel", new CancelServiceOrderDto { Reason = reason }, cancellationToken);
    }

    private async Task<ServiceOrderDto> PostActionAsync(string path, object? payload, CancellationToken cancellationToken)
    {
        await AttachBearerAsync();
        using var response = payload == null
            ? await _httpClient.PostAsync(path, null, cancellationToken)
            : await _httpClient.PostAsJsonAsync(path, payload, cancellationToken);

        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException(await TryReadErrorAsync(response) ?? "Service order action failed.");

        var order = await response.Content.ReadFromJsonAsync<ServiceOrderDto>(cancellationToken: cancellationToken);
        return order ?? throw new InvalidOperationException("Empty response from server.");
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
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
                return null;

            if (doc.RootElement.TryGetProperty("message", out var msg) && msg.ValueKind == JsonValueKind.String)
                return msg.GetString();

            if (doc.RootElement.TryGetProperty("error", out var err) && err.ValueKind == JsonValueKind.String)
                return err.GetString();

            return null;
        }
        catch
        {
            return null;
        }
    }
}
