using ServiceMarketplace.Domain.Enums;

namespace ServiceMarketplace.Application.DTOs;

public class ServiceOrderDto
{
    public Guid Id { get; set; }
    public Guid ServiceRequestId { get; set; }
    public Guid? AcceptedBidId { get; set; }
    public Guid? ServicePackageId { get; set; }
    public string CustomerId { get; set; } = string.Empty;
    public string ProviderId { get; set; } = string.Empty;
    public string RequestTitle { get; set; } = string.Empty;
    public string RequestCategory { get; set; } = string.Empty;
    public string ProviderDisplayName { get; set; } = string.Empty;
    public string? ProviderBusinessName { get; set; }
    public decimal AgreedAmount { get; set; }
    public DateTime ScheduledStartAt { get; set; }
    public int? EstimatedDurationMinutes { get; set; }
    public string ExactLocation { get; set; } = string.Empty;
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public ServiceOrderStatus Status { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? ProviderCompletedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public DateTime? CancelledAt { get; set; }
    public string? CancelledByUserId { get; set; }
    public string? CancellationReason { get; set; }
    public PaymentMethod? PaymentMethod { get; set; }
    public PaymentStatus? PaymentStatus { get; set; }
    public DateTime? PaymentRecordedAt { get; set; }
    public DateTime? PaymentReleasedAt { get; set; }
    public int? ReviewRating { get; set; }
    public string? ReviewFeedback { get; set; }
    public bool? ReviewIsHidden { get; set; }
    public DateTime? ReviewCreatedAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
