using ServiceMarketplace.Application.DTOs;

namespace ServiceMarketplace.Application.Interfaces;

public interface IConversationService
{
    Task<List<ConversationInboxItemDto>> GetInboxAsync(string userId, bool includeArchived = false, int take = 50);
    Task<int> GetUnreadCountAsync(string userId);
    Task<ConversationDetailDto> GetAsync(Guid conversationId, string userId);
    Task<ConversationMessagesPageDto> GetMessagesAsync(Guid conversationId, string userId, DateTime? before = null, int take = 50);
    Task<ConversationMessageDto> SendMessageAsync(Guid conversationId, string senderUserId, SendConversationMessageDto request);
    Task<ConversationReadResultDto> MarkReadAsync(Guid conversationId, string userId);
    Task<ConversationDetailDto> UpdatePreferencesAsync(Guid conversationId, string userId, UpdateConversationPreferencesDto request);
    Task<Guid> EnsureServiceOrderConversationAsync(Guid serviceOrderId, string? systemMessage = null, string? systemMessageKey = null);
    Task AddServiceOrderSystemMessageAsync(Guid serviceOrderId, string body, string eventKey);
    Task<Guid> SyncServiceOrderMessagesAsync(Guid serviceOrderId);
}
