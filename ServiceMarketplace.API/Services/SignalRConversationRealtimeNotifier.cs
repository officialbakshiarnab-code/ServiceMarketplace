using Microsoft.AspNetCore.SignalR;
using ServiceMarketplace.API.Hubs;
using ServiceMarketplace.Application.DTOs;
using ServiceMarketplace.Application.Interfaces;

namespace ServiceMarketplace.API.Services;

public sealed class SignalRConversationRealtimeNotifier(
    IHubContext<MessagingHub, IMessagingClient> hubContext) : IConversationRealtimeNotifier
{
    public async Task MessageCreatedAsync(ConversationRealtimeUpdateDto update, CancellationToken cancellationToken = default)
    {
        await hubContext.Clients.Users(update.ParticipantUserIds).MessageCreated(update);
        await ConversationUpdatedAsync(update.ConversationId, update.ParticipantUserIds, cancellationToken);
    }

    public async Task ConversationUpdatedAsync(Guid conversationId, IReadOnlyList<string> participantUserIds, CancellationToken cancellationToken = default)
    {
        await hubContext.Clients.Users(participantUserIds).ConversationUpdated(conversationId);
    }

    public async Task UnreadCountChangedAsync(string userId, int unreadCount, CancellationToken cancellationToken = default)
    {
        await hubContext.Clients.User(userId).UnreadCountChanged(unreadCount);
    }
}
