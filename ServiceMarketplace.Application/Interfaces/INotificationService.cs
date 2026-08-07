namespace ServiceMarketplace.Application.Interfaces;

// Purpose: Notification contract for request/bid lifecycle events.
public interface INotificationService
{
    Task NotifyNewRequestNearbyAsync(Guid serviceRequestId);
    Task NotifyBidAcceptedAsync(Guid bidId);
    Task NotifyServiceOrderCreatedAsync(Guid orderId);
    Task NotifyServiceOrderStartedAsync(Guid orderId);
    Task NotifyServiceOrderProviderCompletedAsync(Guid orderId);
    Task NotifyServiceOrderCompletedAsync(Guid orderId);
    Task NotifyServiceOrderCancelledAsync(Guid orderId, string cancelledByUserId);
    Task NotifyOrderMessageReceivedAsync(Guid messageId);
    Task NotifyServiceOrderPaymentRecordedAsync(Guid paymentId);
    Task NotifyServiceOrderReviewReceivedAsync(Guid reviewId);
}
