using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using ServiceMarketplace.Application.DTOs;
using ServiceMarketplace.UI.Shared.Auth;

namespace ServiceMarketplace.UI.Shared.Requests;

public sealed class ServiceOrderMessagesApiClient(HttpClient httpClient, ITokenStorage tokenStorage)
{
    private readonly HttpClient _httpClient = httpClient;
    private readonly ITokenStorage _tokenStorage = tokenStorage;

    public async Task<List<ServiceOrderMessageDto>> GetMessagesAsync(Guid orderId, CancellationToken cancellationToken = default)
    {
        await AttachBearerAsync();
        using var response = await _httpClient.GetAsync($"api/orders/{orderId}/messages", cancellationToken);
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException(await TryReadErrorAsync(response) ?? "Failed to load messages.");

        return await response.Content.ReadFromJsonAsync<List<ServiceOrderMessageDto>>(cancellationToken: cancellationToken) ?? [];
    }

    public async Task<ServiceOrderMessageDto> SendAsync(Guid orderId, string body, CancellationToken cancellationToken = default)
    {
        await AttachBearerAsync();
        using var response = await _httpClient.PostAsJsonAsync(
            $"api/orders/{orderId}/messages",
            new CreateServiceOrderMessageDto { Body = body },
            cancellationToken);

        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException(await TryReadErrorAsync(response) ?? "Failed to send message.");

        var message = await response.Content.ReadFromJsonAsync<ServiceOrderMessageDto>(cancellationToken: cancellationToken);
        return message ?? throw new InvalidOperationException("Empty response from server.");
    }

    public async Task MarkThreadReadAsync(Guid orderId, CancellationToken cancellationToken = default)
    {
        await AttachBearerAsync();
        using var response = await _httpClient.PostAsync($"api/orders/{orderId}/messages/read", null, cancellationToken);
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException(await TryReadErrorAsync(response) ?? "Failed to mark messages read.");
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
        }

        return null;
    }
}
