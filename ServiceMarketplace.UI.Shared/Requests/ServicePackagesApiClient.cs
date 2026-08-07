using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using ServiceMarketplace.Application.DTOs;
using ServiceMarketplace.UI.Shared.Auth;

namespace ServiceMarketplace.UI.Shared.Requests;

public sealed class ServicePackagesApiClient(HttpClient httpClient, ITokenStorage tokenStorage)
{
    private readonly HttpClient _httpClient = httpClient;
    private readonly ITokenStorage _tokenStorage = tokenStorage;

    public async Task<List<ServicePackageDto>> GetActiveAsync(Guid? categoryId = null, Guid? zoneId = null, CancellationToken cancellationToken = default)
    {
        await AttachBearerAsync();
        var query = new List<string>();
        if (categoryId.HasValue)
            query.Add($"categoryId={categoryId.Value}");
        if (zoneId.HasValue)
            query.Add($"zoneId={zoneId.Value}");

        var path = query.Count == 0
            ? "api/service-packages"
            : $"api/service-packages?{string.Join("&", query)}";

        using var response = await _httpClient.GetAsync(path, cancellationToken);
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException(await TryReadErrorAsync(response) ?? "Failed to load service packages.");

        return await response.Content.ReadFromJsonAsync<List<ServicePackageDto>>(cancellationToken: cancellationToken) ?? [];
    }

    public async Task<List<ServicePackageDto>> GetMineAsync(CancellationToken cancellationToken = default)
    {
        await AttachBearerAsync();
        using var response = await _httpClient.GetAsync("api/service-packages/mine", cancellationToken);
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException(await TryReadErrorAsync(response) ?? "Failed to load service packages.");

        return await response.Content.ReadFromJsonAsync<List<ServicePackageDto>>(cancellationToken: cancellationToken) ?? [];
    }

    public async Task<ServicePackageDto> CreateAsync(UpsertServicePackageDto dto, CancellationToken cancellationToken = default)
    {
        await AttachBearerAsync();
        using var response = await _httpClient.PostAsJsonAsync("api/service-packages", dto, cancellationToken);
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException(await TryReadErrorAsync(response) ?? "Failed to create service package.");

        return await response.Content.ReadFromJsonAsync<ServicePackageDto>(cancellationToken: cancellationToken)
            ?? throw new InvalidOperationException("Empty response from server.");
    }

    public async Task<ServicePackageDto> UpdateAsync(Guid packageId, UpsertServicePackageDto dto, CancellationToken cancellationToken = default)
    {
        await AttachBearerAsync();
        using var response = await _httpClient.PutAsJsonAsync($"api/service-packages/{packageId}", dto, cancellationToken);
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException(await TryReadErrorAsync(response) ?? "Failed to update service package.");

        return await response.Content.ReadFromJsonAsync<ServicePackageDto>(cancellationToken: cancellationToken)
            ?? throw new InvalidOperationException("Empty response from server.");
    }

    public async Task<ServiceOrderDto> BookAsync(Guid packageId, BookServicePackageDto dto, CancellationToken cancellationToken = default)
    {
        await AttachBearerAsync();
        using var response = await _httpClient.PostAsJsonAsync($"api/service-packages/{packageId}/book", dto, cancellationToken);
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException(await TryReadErrorAsync(response) ?? "Failed to book service package.");

        return await response.Content.ReadFromJsonAsync<ServiceOrderDto>(cancellationToken: cancellationToken)
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
