using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using ServiceMarketplace.Application.DTOs;
using ServiceMarketplace.UI.Shared.Auth;

namespace ServiceMarketplace.UI.Shared.Requests;

public sealed class ServiceOrderPaymentsApiClient(HttpClient httpClient, ITokenStorage tokenStorage)
{
    private readonly HttpClient _httpClient = httpClient;
    private readonly ITokenStorage _tokenStorage = tokenStorage;

    public async Task<ServiceOrderPaymentDto?> GetAsync(Guid orderId, CancellationToken cancellationToken = default)
    {
        await AttachBearerAsync();
        using var response = await _httpClient.GetAsync($"api/orders/{orderId}/payment", cancellationToken);
        if (response.StatusCode == HttpStatusCode.NoContent)
            return null;

        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException(await TryReadErrorAsync(response) ?? "Failed to load payment.");

        return await response.Content.ReadFromJsonAsync<ServiceOrderPaymentDto>(cancellationToken: cancellationToken);
    }

    public async Task<ServiceOrderPaymentDto> RecordAsync(Guid orderId, RecordServiceOrderPaymentDto dto, CancellationToken cancellationToken = default)
    {
        await AttachBearerAsync();
        using var response = await _httpClient.PostAsJsonAsync($"api/orders/{orderId}/payment", dto, cancellationToken);
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException(await TryReadErrorAsync(response) ?? "Failed to record payment.");

        return await response.Content.ReadFromJsonAsync<ServiceOrderPaymentDto>(cancellationToken: cancellationToken)
            ?? throw new InvalidOperationException("Empty response from server.");
    }

    public async Task<PlatformPaymentIntentDto?> GetPlatformIntentAsync(Guid orderId, CancellationToken cancellationToken = default)
    {
        await AttachBearerAsync();
        using var response = await _httpClient.GetAsync($"api/orders/{orderId}/platform-payment-intent", cancellationToken);
        if (response.StatusCode == HttpStatusCode.NoContent)
            return null;

        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException(await TryReadErrorAsync(response) ?? "Failed to load platform payment intent.");

        return await response.Content.ReadFromJsonAsync<PlatformPaymentIntentDto>(cancellationToken: cancellationToken);
    }

    public async Task<PlatformPaymentIntentDto> CreatePlatformIntentAsync(Guid orderId, CancellationToken cancellationToken = default)
    {
        await AttachBearerAsync();
        using var response = await _httpClient.PostAsync($"api/orders/{orderId}/platform-payment-intent", null, cancellationToken);
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException(await TryReadErrorAsync(response) ?? "Failed to start platform payment.");

        return await response.Content.ReadFromJsonAsync<PlatformPaymentIntentDto>(cancellationToken: cancellationToken)
            ?? throw new InvalidOperationException("Empty response from server.");
    }

    public async Task<ProviderPayoutDto?> GetPayoutAsync(Guid orderId, CancellationToken cancellationToken = default)
    {
        await AttachBearerAsync();
        using var response = await _httpClient.GetAsync($"api/orders/{orderId}/payout", cancellationToken);
        if (response.StatusCode == HttpStatusCode.NoContent)
            return null;

        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException(await TryReadErrorAsync(response) ?? "Failed to load payout.");

        return await response.Content.ReadFromJsonAsync<ProviderPayoutDto>(cancellationToken: cancellationToken);
    }

    public async Task<List<ServiceOrderDisputeDto>> GetDisputesAsync(Guid orderId, CancellationToken cancellationToken = default)
    {
        await AttachBearerAsync();
        using var response = await _httpClient.GetAsync($"api/orders/{orderId}/disputes", cancellationToken);
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException(await TryReadErrorAsync(response) ?? "Failed to load disputes.");

        return await response.Content.ReadFromJsonAsync<List<ServiceOrderDisputeDto>>(cancellationToken: cancellationToken) ?? [];
    }

    public async Task<ServiceOrderDisputeDto> CreateDisputeAsync(Guid orderId, CreateServiceOrderDisputeDto dto, CancellationToken cancellationToken = default)
    {
        await AttachBearerAsync();
        using var response = await _httpClient.PostAsJsonAsync($"api/orders/{orderId}/disputes", dto, cancellationToken);
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException(await TryReadErrorAsync(response) ?? "Failed to open dispute.");

        return await response.Content.ReadFromJsonAsync<ServiceOrderDisputeDto>(cancellationToken: cancellationToken)
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
