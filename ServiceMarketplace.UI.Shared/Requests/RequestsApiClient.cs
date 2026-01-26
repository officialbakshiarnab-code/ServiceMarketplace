using System.Net.Http.Headers;
using System.Net.Http.Json;
using ServiceMarketplace.Application.DTOs;
using ServiceMarketplace.UI.Shared.Auth;
using ServiceMarketplace.UI.Shared.Services;

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
        response.EnsureSuccessStatusCode();

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

    private sealed class CreateServiceRequestResponse
    {
        public Guid RequestId { get; set; }
    }
}