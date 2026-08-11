using System.Net.Http.Json;
using System.Text.Json;
using ServiceMarketplace.Application.DTOs;
using ServiceMarketplace.Domain.Enums;

namespace ServiceMarketplace.UI.Shared.Requests;

public sealed class SellerApplicationsApiClient(HttpClient httpClient)
{
    public async Task<SellerApplicationDto?> GetMineAsync(CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.GetAsync("api/seller-applications/me", cancellationToken);
        if (response.StatusCode == System.Net.HttpStatusCode.NoContent)
            return null;

        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException(await TryReadErrorAsync(response) ?? "Failed to load seller application.");

        return await response.Content.ReadFromJsonAsync<SellerApplicationDto>(cancellationToken: cancellationToken);
    }

    public async Task<SellerApplicationDto> UpsertMineAsync(UpsertSellerApplicationDto dto, CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.PutAsJsonAsync("api/seller-applications/me", dto, cancellationToken);
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException(await TryReadErrorAsync(response) ?? "Failed to save seller application.");

        return await response.Content.ReadFromJsonAsync<SellerApplicationDto>(cancellationToken: cancellationToken)
            ?? throw new InvalidOperationException("Empty seller application response.");
    }

    public async Task<SellerApplicationDto> SubmitMineAsync(CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.PostAsync("api/seller-applications/me/submit", null, cancellationToken);
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException(await TryReadErrorAsync(response) ?? "Failed to submit seller application.");

        return await response.Content.ReadFromJsonAsync<SellerApplicationDto>(cancellationToken: cancellationToken)
            ?? throw new InvalidOperationException("Empty seller application response.");
    }

    public async Task<List<SellerApplicationDto>> GetForAdminAsync(SellerApplicationStatus? status = null, CancellationToken cancellationToken = default)
    {
        var url = status.HasValue
            ? $"api/admin/seller-applications?status={(short)status.Value}"
            : "api/admin/seller-applications";

        using var response = await httpClient.GetAsync(url, cancellationToken);
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException(await TryReadErrorAsync(response) ?? "Failed to load seller applications.");

        return await response.Content.ReadFromJsonAsync<List<SellerApplicationDto>>(cancellationToken: cancellationToken) ?? [];
    }

    public async Task<SellerApplicationDto> ReviewAsync(Guid applicationId, ReviewSellerApplicationDto dto, CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.PostAsJsonAsync($"api/admin/seller-applications/{applicationId}/review", dto, cancellationToken);
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException(await TryReadErrorAsync(response) ?? "Failed to review seller application.");

        return await response.Content.ReadFromJsonAsync<SellerApplicationDto>(cancellationToken: cancellationToken)
            ?? throw new InvalidOperationException("Empty seller application response.");
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
