using System.Net.Http.Json;
using System.Text.Json;
using ServiceMarketplace.Application.DTOs;
using ServiceMarketplace.Domain.Enums;

namespace ServiceMarketplace.UI.Shared.Requests;

public sealed class ContactRequestsApiClient(HttpClient httpClient)
{
    public async Task<ContactRequestDto> CreateAsync(CreateContactRequestDto request, CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.PostAsJsonAsync("api/contact-requests", request, cancellationToken);
        return await ReadContactRequestAsync(response, "Failed to create contact request.", cancellationToken);
    }

    public async Task<List<ContactRequestDto>> GetSentAsync(CancellationToken cancellationToken = default)
    {
        return await httpClient.GetFromJsonAsync<List<ContactRequestDto>>("api/contact-requests/sent", cancellationToken) ?? [];
    }

    public async Task<List<ContactRequestDto>> GetReceivedAsync(CancellationToken cancellationToken = default)
    {
        return await httpClient.GetFromJsonAsync<List<ContactRequestDto>>("api/contact-requests/received", cancellationToken) ?? [];
    }

    public async Task<ContactRequestSummaryDto> GetSummaryAsync(CancellationToken cancellationToken = default)
    {
        return await httpClient.GetFromJsonAsync<ContactRequestSummaryDto>("api/contact-requests/summary", cancellationToken)
            ?? new ContactRequestSummaryDto();
    }

    public async Task<List<ContactRequestDto>> GetAdminQueueAsync(ContactRequestStatus? status = null, CancellationToken cancellationToken = default)
    {
        var path = status.HasValue
            ? $"api/admin/contact-requests?status={(int)status.Value}"
            : "api/admin/contact-requests";

        return await httpClient.GetFromJsonAsync<List<ContactRequestDto>>(path, cancellationToken) ?? [];
    }

    public async Task<ContactRequestDto> ReviewAsync(Guid id, ReviewContactRequestDto request, CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.PostAsJsonAsync($"api/admin/contact-requests/{id}/review", request, cancellationToken);
        return await ReadContactRequestAsync(response, "Failed to review contact request.", cancellationToken);
    }

    public async Task<ContactRequestDto> CompleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.PostAsync($"api/contact-requests/{id}/complete", null, cancellationToken);
        return await ReadContactRequestAsync(response, "Failed to complete contact request.", cancellationToken);
    }

    private static async Task<ContactRequestDto> ReadContactRequestAsync(
        HttpResponseMessage response,
        string fallback,
        CancellationToken cancellationToken)
    {
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException(await TryReadErrorAsync(response) ?? fallback);

        return await response.Content.ReadFromJsonAsync<ContactRequestDto>(cancellationToken: cancellationToken)
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
