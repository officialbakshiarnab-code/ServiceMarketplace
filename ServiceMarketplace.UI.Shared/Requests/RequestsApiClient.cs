using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using ServiceMarketplace.Application.DTOs;
using ServiceMarketplace.UI.Shared.Auth;

namespace ServiceMarketplace.UI.Shared.Requests;

public sealed class RequestsApiClient(HttpClient httpClient, ITokenStorage tokenStorage)
{
    // Purpose: API client for service request endpoints (create/open/nearby).
    private readonly HttpClient _httpClient = httpClient;
    private readonly ITokenStorage _tokenStorage = tokenStorage;

    public async Task<Guid> CreateAsync(CreateServiceRequestDto dto, CancellationToken cancellationToken = default)
    {
        await AttachBearerAsync(cancellationToken);

        using var response = await _httpClient.PostAsJsonAsync("api/requests", dto, cancellationToken);
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException(await TryReadErrorAsync(response) ?? "Failed to create request.");

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

    public async Task<List<ServiceRequestDto>> GetMyRequestsAsync(CancellationToken cancellationToken = default)
    {
        await AttachBearerAsync(cancellationToken);

        using var response = await _httpClient.GetAsync("api/requests/mine", cancellationToken);
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException(await TryReadErrorAsync(response) ?? "Failed to load your requests.");

        return await response.Content.ReadFromJsonAsync<List<ServiceRequestDto>>(cancellationToken: cancellationToken) ?? [];
    }

    public async Task<List<ServiceRequestDto>> GetAvailableRequestsAsync(CancellationToken cancellationToken = default)
    {
        await AttachBearerAsync(cancellationToken);

        using var response = await _httpClient.GetAsync("api/requests/available", cancellationToken);
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException(await TryReadErrorAsync(response) ?? "Failed to load available requests.");

        return await response.Content.ReadFromJsonAsync<List<ServiceRequestDto>>(cancellationToken: cancellationToken) ?? [];
    }

    public async Task<ServiceRequestDto> GetByIdAsync(Guid requestId, CancellationToken cancellationToken = default)
    {
        await AttachBearerAsync(cancellationToken);

        using var response = await _httpClient.GetAsync($"api/requests/{requestId}", cancellationToken);
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException(await TryReadErrorAsync(response) ?? "Failed to load request.");

        var request = await response.Content.ReadFromJsonAsync<ServiceRequestDto>(cancellationToken: cancellationToken);
        return request ?? throw new InvalidOperationException("Empty response from server.");
    }

    public async Task<ServiceRequestDto> GetRequestDetailsAsync(Guid requestId, CancellationToken cancellationToken = default)
    {
        await AttachBearerAsync(cancellationToken);

        using var response = await _httpClient.GetAsync($"api/requests/{requestId}/details", cancellationToken);
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException(await TryReadErrorAsync(response) ?? "Failed to load request details.");

        var request = await response.Content.ReadFromJsonAsync<ServiceRequestDto>(cancellationToken: cancellationToken);
        return request ?? throw new InvalidOperationException("Empty response from server.");
    }

    public async Task AcceptBidAsync(Guid requestId, Guid bidId, CancellationToken cancellationToken = default)
    {
        await AttachBearerAsync(cancellationToken);

        using var response = await _httpClient.PostAsync($"api/requests/{requestId}/accept/{bidId}", null, cancellationToken);
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException(await TryReadErrorAsync(response) ?? "Failed to accept bid.");
    }

    /// <summary>
    /// Get dashboard statistics for the current user.
    /// Returns counts of open requests, active bids, and completed requests.
    /// </summary>
    public async Task<UserDashboardStatsDto> GetDashboardStatsAsync(CancellationToken cancellationToken = default)
    {
        await AttachBearerAsync(cancellationToken);

        using var response = await _httpClient.GetAsync("api/requests/stats", cancellationToken);
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException(await TryReadErrorAsync(response) ?? "Failed to load dashboard stats.");

        var stats = await response.Content.ReadFromJsonAsync<UserDashboardStatsDto>(cancellationToken: cancellationToken);
        return stats ?? throw new InvalidOperationException("Empty response from server.");
    }

    private async Task AttachBearerAsync(CancellationToken cancellationToken)
    {
        _ = cancellationToken;
        // AuthorizingHttpClientHandler automatically attaches token
        // This method is kept for backwards compatibility
        // No manual attachment needed here
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

            if (doc.RootElement.TryGetProperty("errors", out var errors) && errors.ValueKind == JsonValueKind.Object)
            {
                var lines = new List<string>();
                foreach (var prop in errors.EnumerateObject())
                {
                    if (prop.Value.ValueKind == JsonValueKind.Array)
                    {
                        var messages = prop.Value.EnumerateArray()
                            .Where(e => e.ValueKind == JsonValueKind.String)
                            .Select(e => e.GetString())
                            .Where(s => !string.IsNullOrWhiteSpace(s));

                        var joined = string.Join("; ", messages!);
                        if (!string.IsNullOrWhiteSpace(joined))
                            lines.Add($"{prop.Name}: {joined}");
                    }
                }

                if (lines.Count > 0)
                    return string.Join(" | ", lines);
            }

            return null;
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