namespace ServiceMarketplace.Application.Interfaces;

// Purpose: Notification contract for request/bid lifecycle events.
public interface INotificationService
{
    Task NotifyNewRequestNearbyAsync(Guid serviceRequestId);
    Task NotifyBidAcceptedAsync(Guid bidId);
}
