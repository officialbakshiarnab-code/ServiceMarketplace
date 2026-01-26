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
            if (string.IsNullOrWhiteSpace(text))
                return null;

            using var doc = JsonDocument.Parse(text);
            if (doc.RootElement.ValueKind == JsonValueKind.Object)
            {
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
            }

            return text;
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