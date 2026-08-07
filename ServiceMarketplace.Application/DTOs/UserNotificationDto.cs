using ServiceMarketplace.Domain.Enums;

namespace ServiceMarketplace.Application.DTOs;

public class UserNotificationDto
{
    public Guid Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public UserNotificationType Type { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public Guid? ServiceOrderId { get; set; }
    public Guid? ServiceRequestId { get; set; }
    public Guid? BidId { get; set; }
    public Guid? ServiceOrderMessageId { get; set; }
    public Guid? ProductDeliveryOrderId { get; set; }
    public Guid? ProviderPayoutId { get; set; }
    public Guid? ServiceOrderDisputeId { get; set; }
    public bool IsRead { get; set; }
    public DateTime? ReadAt { get; set; }
    public DateTime CreatedAt { get; set; }
}
