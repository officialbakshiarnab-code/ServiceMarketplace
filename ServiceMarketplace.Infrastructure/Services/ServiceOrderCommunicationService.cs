using Microsoft.EntityFrameworkCore;
using ServiceMarketplace.Application.DTOs;
using ServiceMarketplace.Application.Exceptions;
using ServiceMarketplace.Application.Interfaces;
using ServiceMarketplace.Domain.Entities;
using ServiceMarketplace.Infrastructure.Data;

namespace ServiceMarketplace.Infrastructure.Services;

public sealed class ServiceOrderCommunicationService(
    AppDbContext context,
    INotificationService notificationService,
    IConversationService conversationService) : IServiceOrderCommunicationService
{
    public async Task<List<ServiceOrderMessageDto>> GetMessagesAsync(Guid orderId, string userId)
    {
        await GetParticipantOrderAsync(orderId, userId);
        await conversationService.SyncServiceOrderMessagesAsync(orderId);

        return await context.ServiceOrderMessages
            .AsNoTracking()
            .Where(m => m.ServiceOrderId == orderId)
            .OrderBy(m => m.CreatedAt)
            .Select(m => ToDto(m))
            .ToListAsync();
    }

    public async Task<ServiceOrderMessageDto> SendMessageAsync(Guid orderId, string senderUserId, CreateServiceOrderMessageDto dto)
    {
        return await context.ExecuteAtomicAsync(async () =>
        {
            var order = await GetParticipantOrderAsync(orderId, senderUserId);
            var body = NormalizeBody(dto.Body);
            var recipientUserId = string.Equals(senderUserId, order.CustomerId, StringComparison.Ordinal)
                ? order.ProviderId
                : order.CustomerId;

            var message = new ServiceOrderMessage
            {
                ServiceOrderId = order.Id,
                SenderUserId = senderUserId,
                RecipientUserId = recipientUserId,
                Body = body,
                CreatedAt = DateTime.UtcNow
            };

            context.ServiceOrderMessages.Add(message);
            await context.SaveChangesAsync();

            var conversationId = await conversationService.EnsureServiceOrderConversationAsync(order.Id);
            await conversationService.SendMessageAsync(conversationId, senderUserId, new SendConversationMessageDto
            {
                Body = body,
                ClientMessageId = ToConversationClientMessageId(message.Id)
            });

            await notificationService.NotifyOrderMessageReceivedAsync(message.Id);
            return ToDto(message);
        });
    }

    public async Task<int> MarkThreadReadAsync(Guid orderId, string userId)
    {
        return await context.ExecuteAtomicAsync(async () =>
        {
            await GetParticipantOrderAsync(orderId, userId);

            var unread = await context.ServiceOrderMessages
                .Where(m => m.ServiceOrderId == orderId && m.RecipientUserId == userId && m.ReadAt == null)
                .ToListAsync();

            foreach (var message in unread)
            {
                message.ReadAt = DateTime.UtcNow;
                message.UpdatedAt = DateTime.UtcNow;
            }

            await context.SaveChangesAsync();
            var conversationId = await conversationService.SyncServiceOrderMessagesAsync(orderId);
            await conversationService.MarkReadAsync(conversationId, userId);
            return unread.Count;
        });
    }

    private async Task<ServiceOrder> GetParticipantOrderAsync(Guid orderId, string userId)
    {
        if (string.IsNullOrWhiteSpace(userId))
            throw new UnauthorizedAccessException("Authentication is required.");

        var order = await context.ServiceOrders
            .AsNoTracking()
            .FirstOrDefaultAsync(o => o.Id == orderId && (o.CustomerId == userId || o.ProviderId == userId));

        if (order == null)
            throw new NotFoundException("Service order not found.");

        return order;
    }

    private static string NormalizeBody(string body)
    {
        if (string.IsNullOrWhiteSpace(body))
            throw new BadRequestException("Message body is required.");

        body = body.Trim();
        if (body.Length > 2000)
            throw new BadRequestException("Message body cannot exceed 2000 characters.");

        return body;
    }

    private static ServiceOrderMessageDto ToDto(ServiceOrderMessage message)
    {
        return new ServiceOrderMessageDto
        {
            Id = message.Id,
            ServiceOrderId = message.ServiceOrderId,
            SenderUserId = message.SenderUserId,
            RecipientUserId = message.RecipientUserId,
            Body = message.Body,
            ReadAt = message.ReadAt,
            CreatedAt = message.CreatedAt
        };
    }

    private static string ToConversationClientMessageId(Guid messageId)
    {
        return $"legacy-service-order-message:{messageId:N}";
    }
}
