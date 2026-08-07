using ServiceMarketplace.Domain.Enums;

namespace ServiceMarketplace.Application.DTOs;

public class ServiceOrderPaymentDto
{
    public Guid Id { get; set; }
    public Guid ServiceOrderId { get; set; }
    public string CustomerId { get; set; } = string.Empty;
    public string ProviderId { get; set; } = string.Empty;
    public Guid? PlatformPaymentIntentId { get; set; }
    public decimal Amount { get; set; }
    public decimal? PlatformFeeAmount { get; set; }
    public decimal? ProviderPayoutAmount { get; set; }
    public PaymentMethod Method { get; set; }
    public PaymentStatus Status { get; set; }
    public string? ReferenceNumber { get; set; }
    public string? Notes { get; set; }
    public string RecordedByUserId { get; set; } = string.Empty;
    public DateTime RecordedAt { get; set; }
    public DateTime? ReleasedAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
