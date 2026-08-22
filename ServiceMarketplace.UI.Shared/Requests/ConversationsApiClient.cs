using System.Net.Http.Json;
using System.Text.Json;
using ServiceMarketplace.Application.DTOs;

namespace ServiceMarketplace.UI.Shared.Requests;

public sealed class ConversationsApiClient(HttpClient httpClient)
{
    private readonly HttpClient _httpClient = httpClient;

    public async Task<List<ConversationInboxItemDto>> GetInboxAsync(bool includeArchived = false, CancellationToken cancellationToken = default)
    {
        using var response = await _httpClient.GetAsync($"api/conversations?includeArchived={includeArchived}", cancellationToken);
        await EnsureSuccessAsync(response, "Failed to load inbox.");

        return await response.Content.ReadFromJsonAsync<List<ConversationInboxItemDto>>(cancellationToken: cancellationToken) ?? [];
    }

    public async Task<ConversationUnreadCountDto> GetUnreadCountAsync(CancellationToken cancellationToken = default)
    {
        using var response = await _httpClient.GetAsync("api/conversations/unread-count", cancellationToken);
        await EnsureSuccessAsync(response, "Failed to load unread count.");

        return await response.Content.ReadFromJsonAsync<ConversationUnreadCountDto>(cancellationToken: cancellationToken)
            ?? new ConversationUnreadCountDto();
    }

    public async Task<ConversationDetailDto> GetAsync(Guid conversationId, CancellationToken cancellationToken = default)
    {
        using var response = await _httpClient.GetAsync($"api/conversations/{conversationId}", cancellationToken);
        await EnsureSuccessAsync(response, "Failed to load conversation.");

        return await response.Content.ReadFromJsonAsync<ConversationDetailDto>(cancellationToken: cancellationToken)
            ?? throw new InvalidOperationException("Empty conversation response.");
    }

    public async Task<ConversationMessagesPageDto> GetMessagesAsync(Guid conversationId, DateTime? before = null, CancellationToken cancellationToken = default)
    {
        var url = before.HasValue
            ? $"api/conversations/{conversationId}/messages?before={Uri.EscapeDataString(before.Value.ToString("O"))}"
            : $"api/conversations/{conversationId}/messages";

        using var response = await _httpClient.GetAsync(url, cancellationToken);
        await EnsureSuccessAsync(response, "Failed to load messages.");

        return await response.Content.ReadFromJsonAsync<ConversationMessagesPageDto>(cancellationToken: cancellationToken)
            ?? new ConversationMessagesPageDto();
    }

    public async Task<ConversationMessageDto> SendAsync(Guid conversationId, string body, CancellationToken cancellationToken = default)
    {
        using var response = await _httpClient.PostAsJsonAsync(
            $"api/conversations/{conversationId}/messages",
            new SendConversationMessageDto
            {
                Body = body,
                ClientMessageId = Guid.NewGuid().ToString("N")
            },
            cancellationToken);

        await EnsureSuccessAsync(response, "Failed to send message.");

        return await response.Content.ReadFromJsonAsync<ConversationMessageDto>(cancellationToken: cancellationToken)
            ?? throw new InvalidOperationException("Empty message response.");
    }

    public async Task MarkReadAsync(Guid conversationId, CancellationToken cancellationToken = default)
    {
        using var response = await _httpClient.PostAsync($"api/conversations/{conversationId}/read", null, cancellationToken);
        await EnsureSuccessAsync(response, "Failed to mark conversation read.");
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
        }

        return null;
    }
}
