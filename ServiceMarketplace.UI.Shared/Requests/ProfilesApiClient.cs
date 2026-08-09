using System.Net.Http.Json;
using System.Text.Json;
using ServiceMarketplace.Application.DTOs;
using ServiceMarketplace.Domain.Enums;

namespace ServiceMarketplace.UI.Shared.Requests;

public sealed class ProfilesApiClient(HttpClient httpClient)
{
    public async Task<ProfileSearchResponse> SearchAsync(ProfileSearchRequest request, CancellationToken cancellationToken = default)
    {
        var query = new List<string>();

        if (!string.IsNullOrWhiteSpace(request.Query))
            query.Add($"query={Uri.EscapeDataString(request.Query)}");
        if (request.ProfileType.HasValue)
            query.Add($"profileType={(int)request.ProfileType.Value}");
        if (request.ServiceCategoryId.HasValue)
            query.Add($"serviceCategoryId={request.ServiceCategoryId.Value}");
        if (request.ZoneId.HasValue)
            query.Add($"zoneId={request.ZoneId.Value}");
        query.Add($"take={request.Take}");

        using var response = await httpClient.GetAsync($"api/profiles/search?{string.Join("&", query)}", cancellationToken);
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException(await TryReadErrorAsync(response) ?? "Failed to search profiles.");

        return await response.Content.ReadFromJsonAsync<ProfileSearchResponse>(cancellationToken: cancellationToken)
            ?? new ProfileSearchResponse();
    }

    public async Task<ProfileDetailDto> GetDetailAsync(ContactProfileType profileType, string userId, CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.GetAsync($"api/profiles/{(int)profileType}/{Uri.EscapeDataString(userId)}", cancellationToken);
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException(await TryReadErrorAsync(response) ?? "Failed to load profile.");

        return await response.Content.ReadFromJsonAsync<ProfileDetailDto>(cancellationToken: cancellationToken)
            ?? throw new InvalidOperationException("Empty response from server.");
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
