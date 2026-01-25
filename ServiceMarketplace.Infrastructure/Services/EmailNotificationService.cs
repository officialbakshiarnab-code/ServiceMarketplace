using Microsoft.Extensions.Logging;
using ServiceMarketplace.Application.Interfaces;

namespace ServiceMarketplace.Infrastructure.Services;

public class EmailNotificationService : INotificationService
{
    private readonly ILogger<EmailNotificationService> _logger;

    public EmailNotificationService(ILogger<EmailNotificationService> logger)
    {
        _logger = logger;
    }

    public Task NotifyNewRequestNearbyAsync(Guid serviceRequestId)
    {
        _logger.LogInformation("[Notification] New service request created: {ServiceRequestId}", serviceRequestId);
        return Task.CompletedTask;
    }

    public Task NotifyBidAcceptedAsync(Guid bidId)
    {
        _logger.LogInformation("[Notification] Bid accepted: {BidId}", bidId);
        return Task.CompletedTask;
    }
}
