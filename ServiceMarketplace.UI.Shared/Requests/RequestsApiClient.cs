using System.Net.Http.Headers;
using System.Net.Http.Json;
using ServiceMarketplace.Application.DTOs;
using ServiceMarketplace.UI.Shared.Auth;

namespace ServiceMarketplace.UI.Shared.Requests;

public sealed class RequestsApiClient(HttpClient httpClient, ITokenStorage tokenStorage)
{
    private readonly HttpClient _httpClient = httpClient;
    private readonly ITokenStorage _tokenStorage = tokenStorage;

    public async Task<Guid> CreateAsync(CreateServiceRequestDto dto, CancellationToken cancellationToken = default)
    {
        await AttachBearerAsync(cancellationToken);

        using var response = await _httpClient.PostAsJsonAsync("api/requests", dto, cancellationToken);
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<CreateServiceRequestResponse>(cancellationToken: cancellationToken);
        if (payload is null)
            throw new InvalidOperationException("Empty response from server.");

        return payload.RequestId;
    }

    public async Task<List<ServiceRequestDto>> NearbyAsync(NearbySearchDto dto, CancellationToken cancellationToken = default)
    {
        await AttachBearerAsync(cancellationToken);

        using var response = await _httpClient.PostAsJsonAsync("api/requests/nearby", dto, cancellationToken);
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException(await TryReadErrorAsync(response) ?? "Failed to load requests.");

        return await response.Content.ReadFromJsonAsync<List<ServiceRequestDto>>(cancellationToken: cancellationToken) ?? [];
    }

    public async Task<List<ServiceRequestDto>> OpenAsync(CancellationToken cancellationToken = default)
    {
        await AttachBearerAsync(cancellationToken);

        using var response = await _httpClient.GetAsync("api/requests/open", cancellationToken);
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException(await TryReadErrorAsync(response) ?? "Failed to load open requests.");

        return await response.Content.ReadFromJsonAsync<List<ServiceRequestDto>>(cancellationToken: cancellationToken) ?? [];
    }

    private async Task AttachBearerAsync(CancellationToken cancellationToken)
    {
        _ = cancellationToken;
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
            return string.IsNullOrWhiteSpace(text) ? null : text;
        }
        catch
        {
            return null;
        }
    }

    private sealed class CreateServiceRequestResponse
    {
        public Guid RequestId { get; set; }
    }
}