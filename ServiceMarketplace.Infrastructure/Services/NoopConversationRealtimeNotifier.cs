using ServiceMarketplace.Application.DTOs;
using ServiceMarketplace.Application.Interfaces;

namespace ServiceMarketplace.Infrastructure.Services;

public sealed class NoopConversationRealtimeNotifier : IConversationRealtimeNotifier
{
    public Task MessageCreatedAsync(ConversationRealtimeUpdateDto update, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public Task ConversationUpdatedAsync(Guid conversationId, IReadOnlyList<string> participantUserIds, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public Task UnreadCountChangedAsync(string userId, int unreadCount, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }
}
