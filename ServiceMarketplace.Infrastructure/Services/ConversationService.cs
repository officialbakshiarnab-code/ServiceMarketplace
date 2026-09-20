using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ServiceMarketplace.Application.DTOs;
using ServiceMarketplace.Application.Exceptions;
using ServiceMarketplace.Application.Interfaces;
using ServiceMarketplace.Domain.Entities;
using ServiceMarketplace.Domain.Enums;
using ServiceMarketplace.Infrastructure.Data;

namespace ServiceMarketplace.Infrastructure.Services;

public sealed class ConversationService(
    AppDbContext context,
    IConversationRealtimeNotifier realtimeNotifier,
    IPushNotificationSender pushNotificationSender,
    ILogger<ConversationService> logger) : IConversationService
{
    private const int MaxMessageLength = 4000;
    private const int MaxMessagesPerMinute = 20;
    private const string SystemSenderUserId = "system";

    public async Task<List<ConversationInboxItemDto>> GetInboxAsync(string userId, bool includeArchived = false, int take = 50)
    {
        if (string.IsNullOrWhiteSpace(userId))
            throw new UnauthorizedAccessException("Authentication is required.");

        take = Math.Clamp(take, 1, 100);

        var participantRows = await context.ConversationParticipants
            .AsNoTracking()
            .Include(p => p.Conversation)
            .Where(p => p.UserId == userId && p.LeftAt == null && (includeArchived || !p.IsArchived))
            .OrderByDescending(p => p.Conversation.LastMessageAt ?? p.Conversation.CreatedAt)
            .Take(take)
            .ToListAsync();

        return await ToInboxItemsAsync(participantRows, userId);
    }

    public async Task<int> GetUnreadCountAsync(string userId)
    {
        if (string.IsNullOrWhiteSpace(userId))
            return 0;

        var participants = await context.ConversationParticipants
            .AsNoTracking()
            .Where(p => p.UserId == userId && p.LeftAt == null && !p.IsArchived)
            .ToListAsync();

        var total = 0;
        foreach (var participant in participants)
            total += await CountUnreadAsync(participant.ConversationId, userId, participant.LastReadAt);

        return total;
    }

    public async Task<ConversationDetailDto> GetAsync(Guid conversationId, string userId)
    {
        var participant = await GetParticipantAsync(conversationId, userId, tracking: false);
        var conversation = await LoadConversationAsync(conversationId);

        return await ToDetailDtoAsync(conversation, participant, userId);
    }

    public async Task<ConversationMessagesPageDto> GetMessagesAsync(Guid conversationId, string userId, DateTime? before = null, int take = 50)
    {
        await GetParticipantAsync(conversationId, userId, tracking: false);
        take = Math.Clamp(take, 1, 100);

        var query = context.Messages
            .AsNoTracking()
            .Where(m => m.ConversationId == conversationId);

        if (before.HasValue)
            query = query.Where(m => m.CreatedAt < before.Value);

        var messages = await query
            .OrderByDescending(m => m.CreatedAt)
            .ThenByDescending(m => m.Id)
            .Take(take)
            .ToListAsync();

        messages.Reverse();

        return new ConversationMessagesPageDto
        {
            Items = await ToMessageDtosAsync(messages, userId),
            NextBefore = messages.Count == take ? messages.First().CreatedAt : null
        };
    }

    public async Task<ConversationMessageDto> SendMessageAsync(Guid conversationId, string senderUserId, SendConversationMessageDto request)
    {
        var participant = await GetParticipantAsync(conversationId, senderUserId, tracking: false);
        var conversation = await LoadConversationAsync(conversationId, tracking: true);

        if (conversation.Status != ConversationStatus.Active)
            throw new BadRequestException("This conversation is not active.");

        var body = NormalizeTextMessage(request.Body);
        var clientMessageId = NormalizeClientMessageId(request.ClientMessageId);

        if (!string.IsNullOrWhiteSpace(clientMessageId))
        {
            var existing = await context.Messages
                .AsNoTracking()
                .FirstOrDefaultAsync(m => m.ConversationId == conversationId && m.ClientMessageId == clientMessageId);

            if (existing != null)
                return (await ToMessageDtosAsync([existing], senderUserId)).Single();
        }

        await EnsureSendRateAllowedAsync(conversationId, senderUserId);

        var message = new Message
        {
            ConversationId = conversationId,
            SenderUserId = senderUserId,
            Type = MessageType.Text,
            Body = body,
            ClientMessageId = clientMessageId,
            CreatedAt = DateTime.UtcNow
        };

        context.Messages.Add(message);
        conversation.LastMessageAt = message.CreatedAt;
        conversation.LastMessageId = message.Id;
        conversation.UpdatedAt = DateTime.UtcNow;

        await context.SaveChangesAsync();

        var participantUserIds = await GetParticipantUserIdsAsync(conversationId);
        await context.PublishAfterCommitAsync("SignalR.MessageCreated", conversationId, () => realtimeNotifier.MessageCreatedAsync(new ConversationRealtimeUpdateDto
        {
            ConversationId = conversationId,
            MessageId = message.Id,
            ParticipantUserIds = participantUserIds
        }));

        foreach (var userId in participantUserIds.Where(id => id != senderUserId))
            await context.PublishAfterCommitAsync("SignalR.UnreadCountChanged", conversationId, async () =>
                await realtimeNotifier.UnreadCountChangedAsync(userId, await GetUnreadCountAsync(userId)));

        await context.PublishAfterCommitAsync("Push.MessageCreated", conversationId, () =>
            SendPushNotificationsAsync(conversationId, message.Id, participantUserIds.Where(id => id != senderUserId)));

        _ = participant;
        return (await ToMessageDtosAsync([message], senderUserId)).Single();
    }

    public async Task<ConversationReadResultDto> MarkReadAsync(Guid conversationId, string userId)
    {
        var participant = await GetParticipantAsync(conversationId, userId, tracking: true);

        var latest = await context.Messages
            .AsNoTracking()
            .Where(m => m.ConversationId == conversationId)
            .OrderByDescending(m => m.CreatedAt)
            .ThenByDescending(m => m.Id)
            .FirstOrDefaultAsync();

        participant.LastReadMessageId = latest?.Id;
        participant.LastReadAt = latest?.CreatedAt ?? DateTime.UtcNow;
        participant.UpdatedAt = DateTime.UtcNow;
        await context.SaveChangesAsync();

        var unread = await CountUnreadAsync(conversationId, userId, participant.LastReadAt);
        await context.PublishAfterCommitAsync("SignalR.UnreadCountChanged", conversationId, async () =>
            await realtimeNotifier.UnreadCountChangedAsync(userId, await GetUnreadCountAsync(userId)));

        return new ConversationReadResultDto
        {
            ConversationId = conversationId,
            LastReadMessageId = participant.LastReadMessageId,
            LastReadAt = participant.LastReadAt,
            UnreadCount = unread
        };
    }

    public async Task<ConversationDetailDto> UpdatePreferencesAsync(Guid conversationId, string userId, UpdateConversationPreferencesDto request)
    {
        var participant = await GetParticipantAsync(conversationId, userId, tracking: true);
        participant.IsMuted = request.IsMuted;
        participant.IsArchived = request.IsArchived;
        participant.UpdatedAt = DateTime.UtcNow;
        await context.SaveChangesAsync();

        var conversation = await LoadConversationAsync(conversationId);
        return await ToDetailDtoAsync(conversation, participant, userId);
    }

    public async Task<Guid> EnsureServiceOrderConversationAsync(Guid serviceOrderId, string? systemMessage = null, string? systemMessageKey = null)
    {
        return await context.ExecuteAtomicAsync(async () =>
        {
            var existing = await context.Conversations
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.ContextType == ConversationContextType.ServiceOrder && c.ServiceOrderId == serviceOrderId);

            if (existing != null)
            {
                if (!string.IsNullOrWhiteSpace(systemMessage) && !string.IsNullOrWhiteSpace(systemMessageKey))
                    await AddSystemMessageIfMissingAsync(existing.Id, systemMessage, systemMessageKey);

                return existing.Id;
            }

            var order = await context.ServiceOrders
                .Include(o => o.ServiceRequest)
                .FirstOrDefaultAsync(o => o.Id == serviceOrderId)
                ?? throw new NotFoundException("Service order not found.");

            var conversation = new Conversation
            {
                ContextType = ConversationContextType.ServiceOrder,
                ServiceOrderId = order.Id,
                Status = ConversationStatus.Active,
                Subject = NormalizeSubject(order.ServiceRequest.Title),
                CreatedAt = DateTime.UtcNow
            };

            conversation.Participants.Add(new ConversationParticipant
            {
                UserId = order.CustomerId,
                ParticipantKind = ConversationParticipantKind.Customer,
                JoinedAt = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow
            });

            conversation.Participants.Add(new ConversationParticipant
            {
                UserId = order.ProviderId,
                ParticipantKind = ConversationParticipantKind.Provider,
                JoinedAt = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow
            });

            context.Conversations.Add(conversation);
            await context.SaveChangesAsync();

            if (!string.IsNullOrWhiteSpace(systemMessage) && !string.IsNullOrWhiteSpace(systemMessageKey))
                await AddSystemMessageIfMissingAsync(conversation.Id, systemMessage, systemMessageKey);

            return conversation.Id;
        });
    }

    public async Task AddServiceOrderSystemMessageAsync(Guid serviceOrderId, string body, string eventKey)
    {
        await context.ExecuteAtomicAsync(async () =>
        {
            var conversationId = await EnsureServiceOrderConversationAsync(serviceOrderId);
            await AddSystemMessageIfMissingAsync(conversationId, body, $"service-order:{serviceOrderId:N}:{eventKey}");
        });
    }

    public async Task<Guid> SyncServiceOrderMessagesAsync(Guid serviceOrderId)
    {
        return await context.ExecuteAtomicAsync(async () =>
        {
            var conversationId = await EnsureServiceOrderConversationAsync(serviceOrderId);

            var legacyMessages = await context.ServiceOrderMessages
                .AsNoTracking()
                .Where(m => m.ServiceOrderId == serviceOrderId)
                .OrderBy(m => m.CreatedAt)
                .ThenBy(m => m.Id)
                .ToListAsync();

            if (legacyMessages.Count == 0)
                return conversationId;

            var legacyClientMessageIds = legacyMessages
                .Select(m => ToLegacyClientMessageId(m.Id))
                .ToList();

            var existingClientMessageIds = await context.Messages
                .AsNoTracking()
                .Where(m => m.ConversationId == conversationId &&
                            m.ClientMessageId != null &&
                            legacyClientMessageIds.Contains(m.ClientMessageId))
                .Select(m => m.ClientMessageId!)
                .ToListAsync();

            var existing = existingClientMessageIds.ToHashSet(StringComparer.Ordinal);
            foreach (var legacyMessage in legacyMessages)
            {
                var clientMessageId = ToLegacyClientMessageId(legacyMessage.Id);
                if (existing.Contains(clientMessageId))
                    continue;

                context.Messages.Add(new Message
                {
                    ConversationId = conversationId,
                    SenderUserId = legacyMessage.SenderUserId,
                    Type = MessageType.Text,
                    Body = NormalizeTextMessage(legacyMessage.Body),
                    ClientMessageId = clientMessageId,
                    CreatedAt = legacyMessage.CreatedAt,
                    UpdatedAt = legacyMessage.UpdatedAt
                });
            }

            await context.SaveChangesAsync();
            await RefreshLastMessageAsync(conversationId);
            return conversationId;
        });
    }

    private async Task AddSystemMessageIfMissingAsync(Guid conversationId, string body, string clientMessageId)
    {
        var normalizedBody = NormalizeSystemMessage(body);

        var exists = await context.Messages.AnyAsync(m =>
            m.ConversationId == conversationId &&
            m.ClientMessageId == clientMessageId);

        if (exists)
            return;

        var conversation = await LoadConversationAsync(conversationId, tracking: true);
        var message = new Message
        {
            ConversationId = conversationId,
            SenderUserId = SystemSenderUserId,
            Type = MessageType.System,
            Body = normalizedBody,
            ClientMessageId = clientMessageId,
            CreatedAt = DateTime.UtcNow
        };

        context.Messages.Add(message);
        conversation.LastMessageAt = message.CreatedAt;
        conversation.LastMessageId = message.Id;
        conversation.UpdatedAt = DateTime.UtcNow;
        await context.SaveChangesAsync();

        var participantUserIds = await GetParticipantUserIdsAsync(conversationId);
        await context.PublishAfterCommitAsync("SignalR.MessageCreated", conversationId, () => realtimeNotifier.MessageCreatedAsync(new ConversationRealtimeUpdateDto
        {
            ConversationId = conversationId,
            MessageId = message.Id,
            ParticipantUserIds = participantUserIds
        }));
    }

    private async Task RefreshLastMessageAsync(Guid conversationId)
    {
        var latest = await context.Messages
            .AsNoTracking()
            .Where(m => m.ConversationId == conversationId)
            .OrderByDescending(m => m.CreatedAt)
            .ThenByDescending(m => m.Id)
            .FirstOrDefaultAsync();

        if (latest == null)
            return;

        var conversation = await LoadConversationAsync(conversationId, tracking: true);
        conversation.LastMessageAt = latest.CreatedAt;
        conversation.LastMessageId = latest.Id;
        conversation.UpdatedAt = DateTime.UtcNow;
        await context.SaveChangesAsync();
    }

    private async Task<ConversationParticipant> GetParticipantAsync(Guid conversationId, string userId, bool tracking)
    {
        if (string.IsNullOrWhiteSpace(userId))
            throw new UnauthorizedAccessException("Authentication is required.");

        var query = context.ConversationParticipants
            .Where(p => p.ConversationId == conversationId && p.UserId == userId && p.LeftAt == null);

        if (!tracking)
            query = query.AsNoTracking();

        var participant = await query.FirstOrDefaultAsync();
        if (participant == null)
            throw new NotFoundException("Conversation not found.");

        return participant;
    }

    private async Task<Conversation> LoadConversationAsync(Guid conversationId, bool tracking = false)
    {
        var query = context.Conversations
            .Include(c => c.Participants)
            .Where(c => c.Id == conversationId);

        if (!tracking)
            query = query.AsNoTracking();

        return await query.FirstOrDefaultAsync()
            ?? throw new NotFoundException("Conversation not found.");
    }

    private async Task<List<string>> GetParticipantUserIdsAsync(Guid conversationId)
    {
        return await context.ConversationParticipants
            .AsNoTracking()
            .Where(p => p.ConversationId == conversationId && p.LeftAt == null)
            .Select(p => p.UserId)
            .ToListAsync();
    }

    private async Task SendPushNotificationsAsync(Guid conversationId, Guid messageId, IEnumerable<string> recipientUserIds)
    {
        var recipients = recipientUserIds
            .Where(id => !string.IsNullOrWhiteSpace(id) && id != SystemSenderUserId)
            .Distinct(StringComparer.Ordinal)
            .ToList();

        if (recipients.Count == 0)
            return;

        var pushRecipients = await context.ConversationParticipants
            .AsNoTracking()
            .Where(p => p.ConversationId == conversationId &&
                        recipients.Contains(p.UserId) &&
                        p.LeftAt == null &&
                        !p.IsMuted)
            .Select(p => p.UserId)
            .ToListAsync();

        if (pushRecipients.Count == 0)
            return;

        var devices = await context.DeviceRegistrations
            .AsNoTracking()
            .Where(d => pushRecipients.Contains(d.UserId) && d.IsActive)
            .ToListAsync();

        if (devices.Count == 0)
            return;

        var payload = new PushNotificationPayloadDto
        {
            Type = "conversation.message",
            ConversationId = conversationId,
            MessageId = messageId
        };

        foreach (var device in devices
            .GroupBy(d => d.DeviceToken, StringComparer.Ordinal)
            .Select(g => g.First()))
        {
            try
            {
                await pushNotificationSender.SendAsync(device.DeviceToken, payload);
            }
            catch (PushNotificationDeliveryException ex) when (ex.ShouldDeactivateDevice)
            {
                await DeactivateDeviceAsync(device.Id);
                logger.LogWarning(
                    ex,
                    "Push notification device deactivated after provider rejected token. DeviceRegistrationId: {DeviceRegistrationId}",
                    device.Id);
            }
            catch (Exception ex)
            {
                logger.LogWarning(
                    ex,
                    "Push notification failed after message persistence. ConversationId: {ConversationId}, MessageId: {MessageId}",
                    conversationId,
                    messageId);
            }
        }
    }

    private async Task DeactivateDeviceAsync(Guid deviceRegistrationId)
    {
        var registration = await context.DeviceRegistrations.FirstOrDefaultAsync(d => d.Id == deviceRegistrationId);
        if (registration == null || !registration.IsActive)
            return;

        registration.IsActive = false;
        registration.UpdatedAt = DateTime.UtcNow;
        await context.SaveChangesAsync();
    }

    private async Task<List<ConversationInboxItemDto>> ToInboxItemsAsync(List<ConversationParticipant> participants, string viewerUserId)
    {
        var conversationIds = participants.Select(p => p.ConversationId).ToList();
        var latestMessages = await context.Messages
            .AsNoTracking()
            .Where(m => conversationIds.Contains(m.ConversationId) && m.Id == m.Conversation.LastMessageId)
            .ToDictionaryAsync(m => m.ConversationId);

        var names = await LoadDisplayNamesAsync(participants
            .SelectMany(p => p.Conversation.Participants.Select(cp => cp.UserId))
            .Where(id => id != SystemSenderUserId)
            .Distinct()
            .ToList());

        var items = new List<ConversationInboxItemDto>();
        foreach (var participant in participants)
        {
            latestMessages.TryGetValue(participant.ConversationId, out var latest);
            var other = participant.Conversation.Participants.FirstOrDefault(p => p.UserId != viewerUserId);

            items.Add(new ConversationInboxItemDto
            {
                Id = participant.ConversationId,
                ContextType = participant.Conversation.ContextType,
                ServiceOrderId = participant.Conversation.ServiceOrderId,
                Subject = participant.Conversation.Subject,
                Status = participant.Conversation.Status,
                OtherParticipantName = other != null && names.TryGetValue(other.UserId, out var otherName)
                    ? otherName
                    : "Marketplace participant",
                LastMessagePreview = latest == null ? null : ToPreview(latest),
                LastMessageAt = participant.Conversation.LastMessageAt,
                UnreadCount = await CountUnreadAsync(participant.ConversationId, viewerUserId, participant.LastReadAt),
                IsMuted = participant.IsMuted,
                IsArchived = participant.IsArchived
            });
        }

        return items;
    }

    private async Task<ConversationDetailDto> ToDetailDtoAsync(Conversation conversation, ConversationParticipant viewer, string viewerUserId)
    {
        var names = await LoadDisplayNamesAsync(conversation.Participants
            .Select(p => p.UserId)
            .Where(id => id != SystemSenderUserId)
            .Distinct()
            .ToList());

        return new ConversationDetailDto
        {
            Id = conversation.Id,
            ContextType = conversation.ContextType,
            ServiceOrderId = conversation.ServiceOrderId,
            Subject = conversation.Subject,
            Status = conversation.Status,
            IsMuted = viewer.IsMuted,
            IsArchived = viewer.IsArchived,
            UnreadCount = await CountUnreadAsync(conversation.Id, viewerUserId, viewer.LastReadAt),
            CreatedAt = conversation.CreatedAt,
            Participants = conversation.Participants
                .OrderBy(p => p.ParticipantKind)
                .Select(p => new ConversationParticipantDto
                {
                    UserId = p.UserId,
                    DisplayName = p.UserId == SystemSenderUserId
                        ? "ServiceMarketplace"
                        : names.GetValueOrDefault(p.UserId, "Marketplace participant"),
                    ParticipantKind = p.ParticipantKind
                })
                .ToList()
        };
    }

    private async Task<List<ConversationMessageDto>> ToMessageDtosAsync(List<Message> messages, string viewerUserId)
    {
        var names = await LoadDisplayNamesAsync(messages
            .Select(m => m.SenderUserId)
            .Where(id => id != SystemSenderUserId)
            .Distinct()
            .ToList());

        return messages.Select(m => new ConversationMessageDto
        {
            Id = m.Id,
            ConversationId = m.ConversationId,
            SenderUserId = m.SenderUserId,
            SenderDisplayName = m.SenderUserId == SystemSenderUserId
                ? "ServiceMarketplace"
                : names.GetValueOrDefault(m.SenderUserId, "Marketplace participant"),
            Type = m.Type,
            Body = m.IsHidden ? "Message hidden by admin." : m.Body,
            IsMine = m.SenderUserId == viewerUserId,
            IsHidden = m.IsHidden,
            CreatedAt = m.CreatedAt
        }).ToList();
    }

    private async Task<Dictionary<string, string>> LoadDisplayNamesAsync(List<string> userIds)
    {
        var parsedIds = userIds
            .Select(id => Guid.TryParse(id, out var parsed) ? parsed : (Guid?)null)
            .Where(id => id.HasValue)
            .Select(id => id!.Value)
            .Distinct()
            .ToList();

        return await context.Users
            .AsNoTracking()
            .Where(u => parsedIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id.ToString(), u => ((u.FirstName + " " + u.LastName).Trim()));
    }

    private async Task<int> CountUnreadAsync(Guid conversationId, string userId, DateTime? lastReadAt)
    {
        var query = context.Messages
            .AsNoTracking()
            .Where(m => m.ConversationId == conversationId && m.SenderUserId != userId);

        if (lastReadAt.HasValue)
            query = query.Where(m => m.CreatedAt > lastReadAt.Value);

        return await query.CountAsync();
    }

    private async Task EnsureSendRateAllowedAsync(Guid conversationId, string senderUserId)
    {
        var since = DateTime.UtcNow.AddMinutes(-1);
        var recentCount = await context.Messages.CountAsync(m =>
            m.ConversationId == conversationId &&
            m.SenderUserId == senderUserId &&
            m.Type == MessageType.Text &&
            m.CreatedAt >= since);

        if (recentCount >= MaxMessagesPerMinute)
            throw new BadRequestException("Too many messages sent in this conversation. Please wait a moment before sending another message.");
    }

    private static string NormalizeTextMessage(string body)
    {
        if (string.IsNullOrWhiteSpace(body))
            throw new BadRequestException("Message body is required.");

        body = body.Trim();
        if (body.Length > MaxMessageLength)
            throw new BadRequestException($"Message body cannot exceed {MaxMessageLength} characters.");

        return body;
    }

    private static string NormalizeSystemMessage(string body)
    {
        var normalized = NormalizeTextMessage(body);
        return normalized.Length <= 500 ? normalized : normalized[..500];
    }

    private static string? NormalizeClientMessageId(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        value = value.Trim();
        return value.Length <= 100 ? value : value[..100];
    }

    private static string NormalizeSubject(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "Service order";

        value = value.Trim();
        return value.Length <= 200 ? value : value[..200];
    }

    private static string ToPreview(Message message)
    {
        if (message.IsHidden)
            return "Message hidden by admin";

        if (message.Type == MessageType.System)
            return message.Body;

        return message.Body.Length <= 120 ? message.Body : message.Body[..120] + "...";
    }

    private static string ToLegacyClientMessageId(Guid messageId)
    {
        return $"legacy-service-order-message:{messageId:N}";
    }
}
