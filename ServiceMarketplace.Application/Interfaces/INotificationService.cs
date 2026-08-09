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
    Task NotifyProductDeliveryOrderCreatedAsync(Guid productDeliveryOrderId);
    Task NotifyProductDeliveryOrderStatusChangedAsync(Guid productDeliveryOrderId);
    Task NotifyPlatformPaymentVerifiedAsync(Guid serviceOrderPaymentId);
    Task NotifyServiceOrderDisputeOpenedAsync(Guid disputeId);
    Task NotifyServiceOrderDisputeResolvedAsync(Guid disputeId);
    Task NotifyProviderPayoutCreatedAsync(Guid payoutId);
    Task NotifyContactRequestSubmittedAsync(Guid contactRequestId);
    Task NotifyContactRequestApprovedAsync(Guid contactRequestId);
    Task NotifyContactRequestRejectedAsync(Guid contactRequestId);
    Task NotifyCallbackRequestedAsync(Guid contactRequestId);
}
