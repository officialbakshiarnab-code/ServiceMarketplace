namespace ServiceMarketplace.Application.Interfaces;

public interface INotificationService
{
    Task NotifyNewRequestNearbyAsync(Guid serviceRequestId);
    Task NotifyBidAcceptedAsync(Guid bidId);
}
