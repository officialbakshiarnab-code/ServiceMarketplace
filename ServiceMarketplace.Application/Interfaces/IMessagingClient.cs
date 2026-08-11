using ServiceMarketplace.Application.DTOs;

namespace ServiceMarketplace.Application.Interfaces;

public interface IMessagingClient
{
    Task MessageCreated(ConversationRealtimeUpdateDto update);
    Task ConversationUpdated(Guid conversationId);
    Task UnreadCountChanged(int unreadCount);
}
