using ServiceMarketplace.Application.DTOs;

namespace ServiceMarketplace.Application.Interfaces;

public interface IConversationRealtimeNotifier
{
    Task MessageCreatedAsync(ConversationRealtimeUpdateDto update, CancellationToken cancellationToken = default);
    Task ConversationUpdatedAsync(Guid conversationId, IReadOnlyList<string> participantUserIds, CancellationToken cancellationToken = default);
    Task UnreadCountChangedAsync(string userId, int unreadCount, CancellationToken cancellationToken = default);
}
