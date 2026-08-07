using System.Net.Http.Json;
using System.Text.Json;
using ServiceMarketplace.Application.DTOs;
using ServiceMarketplace.Domain.Enums;

namespace ServiceMarketplace.UI.Shared.Requests;

public sealed class ProviderApplicationsApiClient(HttpClient httpClient)
{
    public async Task<ProviderApplicationDto?> GetMineAsync(CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.GetAsync("api/provider-applications/me", cancellationToken);
        if (response.StatusCode == System.Net.HttpStatusCode.NoContent)
            return null;

        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException(await TryReadErrorAsync(response) ?? "Failed to load provider application.");

        return await response.Content.ReadFromJsonAsync<ProviderApplicationDto>(cancellationToken: cancellationToken);
    }

    public async Task<ProviderApplicationDto> UpsertMineAsync(UpsertProviderApplicationDto dto, CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.PutAsJsonAsync("api/provider-applications/me", dto, cancellationToken);
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException(await TryReadErrorAsync(response) ?? "Failed to save provider application.");

        return await response.Content.ReadFromJsonAsync<ProviderApplicationDto>(cancellationToken: cancellationToken)
            ?? throw new InvalidOperationException("Empty provider application response.");
    }

    public async Task<ProviderApplicationDto> SubmitMineAsync(CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.PostAsync("api/provider-applications/me/submit", null, cancellationToken);
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException(await TryReadErrorAsync(response) ?? "Failed to submit provider application.");

        return await response.Content.ReadFromJsonAsync<ProviderApplicationDto>(cancellationToken: cancellationToken)
            ?? throw new InvalidOperationException("Empty provider application response.");
    }

    public async Task<List<ProviderApplicationDto>> GetForAdminAsync(ProviderApplicationStatus? status = null, CancellationToken cancellationToken = default)
    {
        var url = status.HasValue
            ? $"api/admin/provider-applications?status={(short)status.Value}"
            : "api/admin/provider-applications";

        using var response = await httpClient.GetAsync(url, cancellationToken);
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException(await TryReadErrorAsync(response) ?? "Failed to load provider applications.");

        return await response.Content.ReadFromJsonAsync<List<ProviderApplicationDto>>(cancellationToken: cancellationToken) ?? [];
    }

    public async Task<ProviderApplicationDto> ReviewAsync(Guid applicationId, ReviewProviderApplicationDto dto, CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.PostAsJsonAsync($"api/admin/provider-applications/{applicationId}/review", dto, cancellationToken);
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException(await TryReadErrorAsync(response) ?? "Failed to review provider application.");

        return await response.Content.ReadFromJsonAsync<ProviderApplicationDto>(cancellationToken: cancellationToken)
            ?? throw new InvalidOperationException("Empty provider application response.");
    }

    private static async Task<string?> TryReadErrorAsync(HttpResponseMessage response)
    {
        try
        {
            var text = await response.Content.ReadAsStringAsync();
            if (string.IsNullOrWhiteSpace(text))
                return null;

            using var doc = JsonDocument.Parse(text);
            if (doc.RootElement.TryGetProperty("title", out var title) && title.ValueKind == JsonValueKind.String)
                return title.GetString();
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
