using ServiceMarketplace.Domain.Enums;

namespace ServiceMarketplace.Domain.Entities;

public class ServiceOrder : BaseAuditableEntity
{
    public Guid ServiceRequestId { get; set; }
    public ServiceRequest ServiceRequest { get; set; } = null!;

    public Guid? AcceptedBidId { get; set; }
    public Bid? AcceptedBid { get; set; }

    public Guid? ServicePackageId { get; set; }
    public ServicePackage? ServicePackage { get; set; }

    public string CustomerId { get; set; } = string.Empty;
    public string ProviderId { get; set; } = string.Empty;

    public decimal AgreedAmount { get; set; }
    public DateTime ScheduledStartAt { get; set; }
    public int? EstimatedDurationMinutes { get; set; }

    public ServiceOrderStatus Status { get; set; } = ServiceOrderStatus.PendingStart;
    public DateTime? StartedAt { get; set; }
    public DateTime? ProviderCompletedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public DateTime? CancelledAt { get; set; }
    public string? CancelledByUserId { get; set; }
    public string? CancellationReason { get; set; }

    public ICollection<ServiceOrderMessage> Messages { get; set; } = new List<ServiceOrderMessage>();
    public ICollection<ServiceOrderAuditEvent> AuditEvents { get; set; } = new List<ServiceOrderAuditEvent>();
    public ServiceOrderPayment? Payment { get; set; }
    public ServiceOrderReview? Review { get; set; }
}
