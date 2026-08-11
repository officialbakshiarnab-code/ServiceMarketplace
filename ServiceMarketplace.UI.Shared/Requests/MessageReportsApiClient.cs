using System.Net.Http.Json;
using System.Text.Json;
using ServiceMarketplace.Application.DTOs;
using ServiceMarketplace.Domain.Enums;

namespace ServiceMarketplace.UI.Shared.Requests;

public sealed class MessageReportsApiClient(HttpClient httpClient)
{
    private readonly HttpClient _httpClient = httpClient;

    public async Task<MessageReportDto> ReportAsync(
        Guid conversationId,
        Guid messageId,
        CreateMessageReportDto request,
        CancellationToken cancellationToken = default)
    {
        using var response = await _httpClient.PostAsJsonAsync(
            $"api/conversations/{conversationId}/messages/{messageId}/reports",
            request,
            cancellationToken);

        await EnsureSuccessAsync(response, "Failed to report message.");
        return await response.Content.ReadFromJsonAsync<MessageReportDto>(cancellationToken: cancellationToken)
            ?? throw new InvalidOperationException("Empty response from server.");
    }

    public async Task<List<MessageReportDto>> GetAdminQueueAsync(
        MessageReportStatus? status = null,
        CancellationToken cancellationToken = default)
    {
        var url = status.HasValue
            ? $"api/admin/message-reports?status={(int)status.Value}"
            : "api/admin/message-reports";

        using var response = await _httpClient.GetAsync(url, cancellationToken);
        await EnsureSuccessAsync(response, "Failed to load message reports.");
        return await response.Content.ReadFromJsonAsync<List<MessageReportDto>>(cancellationToken: cancellationToken) ?? [];
    }

    public async Task<MessageReportDto> ReviewAsync(
        Guid reportId,
        ReviewMessageReportDto request,
        CancellationToken cancellationToken = default)
    {
        using var response = await _httpClient.PostAsJsonAsync(
            $"api/admin/message-reports/{reportId}/review",
            request,
            cancellationToken);

        await EnsureSuccessAsync(response, "Failed to review message report.");
        return await response.Content.ReadFromJsonAsync<MessageReportDto>(cancellationToken: cancellationToken)
            ?? throw new InvalidOperationException("Empty response from server.");
    }

    private static async Task EnsureSuccessAsync(HttpResponseMessage response, string fallback)
    {
        if (response.IsSuccessStatusCode)
            return;

        throw new InvalidOperationException(await TryReadErrorAsync(response) ?? fallback);
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
            return null;
        }

        return null;
    }
}
