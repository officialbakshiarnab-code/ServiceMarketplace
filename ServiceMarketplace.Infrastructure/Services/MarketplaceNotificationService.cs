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

    public async Task NotifyProductDeliveryOrderCreatedAsync(Guid productDeliveryOrderId)
    {
        var order = await LoadProductDeliveryOrderAsync(productDeliveryOrderId);
        if (order == null)
            return;

        AddNotification(
            order.SellerId.ToString(),
            UserNotificationType.ProductDeliveryOrderCreated,
            "New product delivery order",
            $"New delivery order for {order.ProductListing.Title}.",
            productDeliveryOrderId: order.Id);

        AddNotification(
            order.BuyerId,
            UserNotificationType.ProductDeliveryOrderCreated,
            "Product order placed",
            $"Your delivery order for {order.ProductListing.Title} was sent to the seller.",
            productDeliveryOrderId: order.Id);

        await context.SaveChangesAsync();
    }

    public async Task NotifyProductDeliveryOrderStatusChangedAsync(Guid productDeliveryOrderId)
    {
        var order = await LoadProductDeliveryOrderAsync(productDeliveryOrderId);
        if (order == null)
            return;

        var statusLabel = ToProductDeliveryStatusLabel(order.Status);

        AddNotification(
            order.BuyerId,
            UserNotificationType.ProductDeliveryOrderStatusChanged,
            "Product delivery updated",
            $"{order.ProductListing.Title} is now {statusLabel}.",
            productDeliveryOrderId: order.Id);

        AddNotification(
            order.SellerId.ToString(),
            UserNotificationType.ProductDeliveryOrderStatusChanged,
            "Product delivery updated",
            $"Delivery order for {order.ProductListing.Title} is now {statusLabel}.",
            productDeliveryOrderId: order.Id);

        await context.SaveChangesAsync();
    }

    public async Task NotifyPlatformPaymentVerifiedAsync(Guid serviceOrderPaymentId)
    {
        var payment = await context.ServiceOrderPayments
            .AsNoTracking()
            .Include(p => p.ServiceOrder)
            .ThenInclude(o => o.ServiceRequest)
            .FirstOrDefaultAsync(p => p.Id == serviceOrderPaymentId);

        if (payment == null)
            return;

        AddNotification(
            payment.CustomerId,
            UserNotificationType.PlatformPaymentVerified,
            "Platform payment verified",
            $"Platform payment was verified for {payment.ServiceOrder.ServiceRequest.Title}.",
            payment.ServiceOrderId);

        AddNotification(
            payment.ProviderId,
            UserNotificationType.PlatformPaymentVerified,
            "Platform payment held",
            $"Platform payment is held for {payment.ServiceOrder.ServiceRequest.Title}.",
            payment.ServiceOrderId);

        await context.SaveChangesAsync();
    }

    public async Task NotifyServiceOrderDisputeOpenedAsync(Guid disputeId)
    {
        var dispute = await LoadDisputeAsync(disputeId);
        if (dispute == null)
            return;

        AddNotification(
            dispute.AgainstUserId,
            UserNotificationType.ServiceOrderDisputeOpened,
            "Service order dispute opened",
            $"A dispute was opened for {dispute.ServiceOrder.ServiceRequest.Title}.",
            dispute.ServiceOrderId,
            serviceOrderDisputeId: dispute.Id);

        await context.SaveChangesAsync();
    }

    public async Task NotifyServiceOrderDisputeResolvedAsync(Guid disputeId)
    {
        var dispute = await LoadDisputeAsync(disputeId);
        if (dispute == null)
            return;

        AddNotification(
            dispute.RaisedByUserId,
            UserNotificationType.ServiceOrderDisputeResolved,
            "Service order dispute resolved",
            $"Dispute resolved for {dispute.ServiceOrder.ServiceRequest.Title}.",
            dispute.ServiceOrderId,
            serviceOrderDisputeId: dispute.Id);

        AddNotification(
            dispute.AgainstUserId,
            UserNotificationType.ServiceOrderDisputeResolved,
            "Service order dispute resolved",
            $"Dispute resolved for {dispute.ServiceOrder.ServiceRequest.Title}.",
            dispute.ServiceOrderId,
            serviceOrderDisputeId: dispute.Id);

        await context.SaveChangesAsync();
    }

    public async Task NotifyProviderPayoutCreatedAsync(Guid payoutId)
    {
        var payout = await context.ProviderPayouts
            .AsNoTracking()
            .Include(p => p.ServiceOrder)
            .ThenInclude(o => o.ServiceRequest)
            .FirstOrDefaultAsync(p => p.Id == payoutId);

        if (payout == null)
            return;

        AddNotification(
            payout.ProviderId,
            UserNotificationType.ProviderPayoutCreated,
            "Provider payout pending",
            $"Payout of {payout.PayoutAmount:0.00} is pending for {payout.ServiceOrder.ServiceRequest.Title}.",
            payout.ServiceOrderId,
            providerPayoutId: payout.Id);

        await context.SaveChangesAsync();
    }

    public async Task NotifyContactRequestSubmittedAsync(Guid contactRequestId)
    {
        var request = await LoadContactRequestAsync(contactRequestId);
        if (request == null)
            return;

        AddNotification(
            request.RequesterUserId,
            UserNotificationType.ContactRequestSubmitted,
            "Contact request submitted",
            "Your contact request is pending admin review.",
            contactRequestId: request.Id);

        AddNotification(
            request.TargetUserId,
            UserNotificationType.ContactRequestSubmitted,
            "Contact request received",
            "A marketplace member requested your contact details. Admin review is pending.",
            contactRequestId: request.Id);

        await context.SaveChangesAsync();
    }

    public async Task NotifyContactRequestApprovedAsync(Guid contactRequestId)
    {
        var request = await LoadContactRequestAsync(contactRequestId);
        if (request == null)
            return;

        AddNotification(
            request.RequesterUserId,
            UserNotificationType.ContactRequestApproved,
            "Contact details available",
            "Admin approved your contact request. Contact details are now available.",
            contactRequestId: request.Id);

        AddNotification(
            request.TargetUserId,
            UserNotificationType.ContactRequestApproved,
            "Contact request approved",
            "Admin approved a contact request connected to your profile.",
            contactRequestId: request.Id);

        await context.SaveChangesAsync();
    }

    public async Task NotifyContactRequestRejectedAsync(Guid contactRequestId)
    {
        var request = await LoadContactRequestAsync(contactRequestId);
        if (request == null)
            return;

        AddNotification(
            request.RequesterUserId,
            UserNotificationType.ContactRequestRejected,
            "Contact request rejected",
            "Admin rejected your contact request.",
            contactRequestId: request.Id);

        await context.SaveChangesAsync();
    }

    public async Task NotifyCallbackRequestedAsync(Guid contactRequestId)
    {
        var request = await LoadContactRequestAsync(contactRequestId);
        if (request == null)
            return;

        AddNotification(
            request.RequesterUserId,
            UserNotificationType.CallbackRequested,
            "Callback request submitted",
            "Your callback request is pending admin review.",
            contactRequestId: request.Id);

        AddNotification(
            request.TargetUserId,
            UserNotificationType.CallbackRequested,
            "Callback requested",
            "A marketplace member requested a callback. Admin review is pending.",
            contactRequestId: request.Id);

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

    private async Task<ProductDeliveryOrder?> LoadProductDeliveryOrderAsync(Guid orderId)
    {
        return await context.ProductDeliveryOrders
            .AsNoTracking()
            .Include(o => o.ProductListing)
            .FirstOrDefaultAsync(o => o.Id == orderId);
    }

    private async Task<ServiceOrderDispute?> LoadDisputeAsync(Guid disputeId)
    {
        return await context.ServiceOrderDisputes
            .AsNoTracking()
            .Include(d => d.ServiceOrder)
            .ThenInclude(o => o.ServiceRequest)
            .FirstOrDefaultAsync(d => d.Id == disputeId);
    }

    private async Task<ContactRequest?> LoadContactRequestAsync(Guid contactRequestId)
    {
        return await context.ContactRequests
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == contactRequestId);
    }

    private void AddNotification(
        string userId,
        UserNotificationType type,
        string title,
        string message,
        Guid? serviceOrderId = null,
        Guid? serviceRequestId = null,
        Guid? bidId = null,
        Guid? serviceOrderMessageId = null,
        Guid? productDeliveryOrderId = null,
        Guid? providerPayoutId = null,
        Guid? serviceOrderDisputeId = null,
        Guid? contactRequestId = null)
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
            ProductDeliveryOrderId = productDeliveryOrderId,
            ProviderPayoutId = providerPayoutId,
            ServiceOrderDisputeId = serviceOrderDisputeId,
            ContactRequestId = contactRequestId,
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
            ProductDeliveryOrderId = notification.ProductDeliveryOrderId,
            ProviderPayoutId = notification.ProviderPayoutId,
            ServiceOrderDisputeId = notification.ServiceOrderDisputeId,
            ContactRequestId = notification.ContactRequestId,
            IsRead = notification.IsRead,
            ReadAt = notification.ReadAt,
            CreatedAt = notification.CreatedAt
        };
    }

    private static string ToProductDeliveryStatusLabel(ProductDeliveryStatus status) => status switch
    {
        ProductDeliveryStatus.PendingSellerConfirmation => "pending seller confirmation",
        ProductDeliveryStatus.Confirmed => "confirmed",
        ProductDeliveryStatus.ReadyForPickup => "ready for pickup",
        ProductDeliveryStatus.OutForDelivery => "out for delivery",
        ProductDeliveryStatus.Delivered => "delivered",
        ProductDeliveryStatus.Cancelled => "cancelled",
        _ => status.ToString()
    };
}
