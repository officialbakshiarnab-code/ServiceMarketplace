using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ServiceMarketplace.Application.DTOs;
using ServiceMarketplace.Application.Exceptions;
using ServiceMarketplace.Application.Interfaces;
using ServiceMarketplace.Domain.Entities;
using ServiceMarketplace.Domain.Enums;
using ServiceMarketplace.Infrastructure.Data;

namespace ServiceMarketplace.Infrastructure.Services;

public sealed class MarketplaceNotificationService(
    AppDbContext context,
    ILogger<MarketplaceNotificationService> logger) : INotificationService, INotificationInboxService
{
    public async Task NotifyNewRequestNearbyAsync(Guid serviceRequestId)
    {
        logger.LogInformation("[Notification] New service request created: {ServiceRequestId}", serviceRequestId);
        await Task.CompletedTask;
    }

    public async Task NotifyBidAcceptedAsync(Guid bidId)
    {
        var bid = await context.Bids
            .AsNoTracking()
            .Include(b => b.ServiceRequest)
            .FirstOrDefaultAsync(b => b.Id == bidId);

        if (bid == null)
            return;

        AddNotification(
            bid.ServiceProviderId,
            UserNotificationType.BidAccepted,
            "Bid accepted",
            $"Your bid was accepted for {bid.ServiceRequest.Title}.",
            serviceRequestId: bid.ServiceRequestId,
            bidId: bid.Id);

        await context.SaveChangesAsync();
        logger.LogInformation("[Notification] Bid accepted: {BidId}", bidId);
    }

    public async Task NotifyServiceOrderCreatedAsync(Guid orderId)
    {
        var order = await LoadOrderAsync(orderId);
        if (order == null)
            return;

        AddNotification(
            order.CustomerId,
            UserNotificationType.ServiceOrderCreated,
            "Service order created",
            $"Service order created for {order.ServiceRequest.Title}.",
            order.Id,
            order.ServiceRequestId,
            order.AcceptedBidId);

        AddNotification(
            order.ProviderId,
            UserNotificationType.ServiceOrderCreated,
            "New service order",
            $"You have a new service order for {order.ServiceRequest.Title}.",
            order.Id,
            order.ServiceRequestId,
            order.AcceptedBidId);

        await context.SaveChangesAsync();
    }

    public async Task NotifyServiceOrderStartedAsync(Guid orderId)
    {
        var order = await LoadOrderAsync(orderId);
        if (order == null)
            return;

        AddNotification(
            order.CustomerId,
            UserNotificationType.ServiceOrderStarted,
            "Service order started",
            $"Work has started for {order.ServiceRequest.Title}.",
            order.Id,
            order.ServiceRequestId,
            order.AcceptedBidId);

        await context.SaveChangesAsync();
    }

    public async Task NotifyServiceOrderProviderCompletedAsync(Guid orderId)
    {
        var order = await LoadOrderAsync(orderId);
        if (order == null)
            return;

        AddNotification(
            order.CustomerId,
            UserNotificationType.ServiceOrderProviderCompleted,
            "Provider marked work complete",
            $"Please confirm completion for {order.ServiceRequest.Title}.",
            order.Id,
            order.ServiceRequestId,
            order.AcceptedBidId);

        await context.SaveChangesAsync();
    }

    public async Task NotifyServiceOrderCompletedAsync(Guid orderId)
    {
        var order = await LoadOrderAsync(orderId);
        if (order == null)
            return;

        AddNotification(
            order.ProviderId,
            UserNotificationType.ServiceOrderCompleted,
            "Service order completed",
            $"The customer confirmed completion for {order.ServiceRequest.Title}.",
            order.Id,
            order.ServiceRequestId,
            order.AcceptedBidId);

        await context.SaveChangesAsync();
    }

    public async Task NotifyServiceOrderCancelledAsync(Guid orderId, string cancelledByUserId)
    {
        var order = await LoadOrderAsync(orderId);
        if (order == null)
            return;

        var recipientId = string.Equals(cancelledByUserId, order.CustomerId, StringComparison.Ordinal)
            ? order.ProviderId
            : order.CustomerId;

        AddNotification(
            recipientId,
            UserNotificationType.ServiceOrderCancelled,
            "Service order cancelled",
            $"Service order cancelled for {order.ServiceRequest.Title}.",
            order.Id,
            order.ServiceRequestId,
            order.AcceptedBidId);

        await context.SaveChangesAsync();
    }

    public async Task NotifyOrderMessageReceivedAsync(Guid messageId)
    {
        var message = await context.ServiceOrderMessages
            .AsNoTracking()
            .Include(m => m.ServiceOrder)
            .ThenInclude(o => o.ServiceRequest)
            .FirstOrDefaultAsync(m => m.Id == messageId);

        if (message == null)
            return;

        AddNotification(
            message.RecipientUserId,
            UserNotificationType.OrderMessageReceived,
            "New order message",
            $"New message for {message.ServiceOrder.ServiceRequest.Title}.",
            message.ServiceOrderId,
            message.ServiceOrder.ServiceRequestId,
            message.ServiceOrder.AcceptedBidId,
            messageId);

        await context.SaveChangesAsync();
    }

    public async Task NotifyServiceOrderPaymentRecordedAsync(Guid paymentId)
    {
        var payment = await context.ServiceOrderPayments
            .AsNoTracking()
            .Include(p => p.ServiceOrder)
            .ThenInclude(o => o.ServiceRequest)
            .FirstOrDefaultAsync(p => p.Id == paymentId);

        if (payment == null)
            return;

        AddNotification(
            payment.ProviderId,
            UserNotificationType.ServiceOrderPaymentRecorded,
            "Payment recorded",
            $"Payment was recorded for {payment.ServiceOrder.ServiceRequest.Title}.",
            payment.ServiceOrderId,
            payment.ServiceOrder.ServiceRequestId,
            payment.ServiceOrder.AcceptedBidId);

        await context.SaveChangesAsync();
    }

    public async Task NotifyServiceOrderReviewReceivedAsync(Guid reviewId)
    {
        var review = await context.ServiceOrderReviews
            .AsNoTracking()
            .Include(r => r.ServiceOrder)
            .ThenInclude(o => o.ServiceRequest)
            .FirstOrDefaultAsync(r => r.Id == reviewId);

        if (review == null)
            return;

        AddNotification(
            review.ProviderId,
            UserNotificationType.ServiceOrderReviewReceived,
            "Review received",
            $"You received a {review.Rating}-star review for {review.ServiceOrder.ServiceRequest.Title}.",
            review.ServiceOrderId,
            review.ServiceOrder.ServiceRequestId,
            review.ServiceOrder.AcceptedBidId);

        await context.SaveChangesAsync();
    }

    public async Task<List<UserNotificationDto>> GetMineAsync(string userId, bool unreadOnly = false)
    {
        if (string.IsNullOrWhiteSpace(userId))
            return [];

        var query = context.UserNotifications
            .AsNoTracking()
            .Where(n => n.UserId == userId);

        if (unreadOnly)
            query = query.Where(n => !n.IsRead);

        return await query
            .OrderByDescending(n => n.CreatedAt)
            .Take(100)
            .Select(n => ToDto(n))
            .ToListAsync();
    }

    public async Task<UserNotificationDto> MarkReadAsync(Guid notificationId, string userId)
    {
        var notification = await context.UserNotifications
            .FirstOrDefaultAsync(n => n.Id == notificationId && n.UserId == userId)
            ?? throw new NotFoundException("Notification not found.");

        if (!notification.IsRead)
        {
            notification.IsRead = true;
            notification.ReadAt = DateTime.UtcNow;
            notification.UpdatedAt = DateTime.UtcNow;
            await context.SaveChangesAsync();
        }

        return ToDto(notification);
    }

    public async Task<int> MarkAllReadAsync(string userId)
    {
        if (string.IsNullOrWhiteSpace(userId))
            return 0;

        var unread = await context.UserNotifications
            .Where(n => n.UserId == userId && !n.IsRead)
            .ToListAsync();

        foreach (var notification in unread)
        {
            notification.IsRead = true;
            notification.ReadAt = DateTime.UtcNow;
            notification.UpdatedAt = DateTime.UtcNow;
        }

        await context.SaveChangesAsync();
        return unread.Count;
    }

    private async Task<ServiceOrder?> LoadOrderAsync(Guid orderId)
    {
        return await context.ServiceOrders
            .AsNoTracking()
            .Include(o => o.ServiceRequest)
            .FirstOrDefaultAsync(o => o.Id == orderId);
    }

    private void AddNotification(
        string userId,
        UserNotificationType type,
        string title,
        string message,
        Guid? serviceOrderId = null,
        Guid? serviceRequestId = null,
        Guid? bidId = null,
        Guid? serviceOrderMessageId = null)
    {
        context.UserNotifications.Add(new UserNotification
        {
            UserId = userId,
            Type = type,
            Title = title,
            Message = message,
            ServiceOrderId = serviceOrderId,
            ServiceRequestId = serviceRequestId,
            BidId = bidId,
            ServiceOrderMessageId = serviceOrderMessageId,
            CreatedAt = DateTime.UtcNow
        });
    }

    private static UserNotificationDto ToDto(UserNotification notification)
    {
        return new UserNotificationDto
        {
            Id = notification.Id,
            UserId = notification.UserId,
            Type = notification.Type,
            Title = notification.Title,
            Message = notification.Message,
            ServiceOrderId = notification.ServiceOrderId,
            ServiceRequestId = notification.ServiceRequestId,
            BidId = notification.BidId,
            ServiceOrderMessageId = notification.ServiceOrderMessageId,
            IsRead = notification.IsRead,
            ReadAt = notification.ReadAt,
            CreatedAt = notification.CreatedAt
        };
    }
}
