using ServiceMarketplace.Domain.Enums;

namespace ServiceMarketplace.Domain.Entities;

public class ServiceOrderPayment : BaseAuditableEntity
{
    public Guid ServiceOrderId { get; set; }
    public ServiceOrder ServiceOrder { get; set; } = null!;

    public Guid? PlatformPaymentIntentId { get; set; }
    public PlatformPaymentIntent? PlatformPaymentIntent { get; set; }

    public string CustomerId { get; set; } = string.Empty;
    public string ProviderId { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public decimal? PlatformFeeAmount { get; set; }
    public decimal? ProviderPayoutAmount { get; set; }
    public PaymentMethod Method { get; set; }
    public PaymentStatus Status { get; set; } = PaymentStatus.Pending;
    public string? ReferenceNumber { get; set; }
    public string? Notes { get; set; }
    public string RecordedByUserId { get; set; } = string.Empty;
    public DateTime RecordedAt { get; set; }
    public DateTime? ReleasedAt { get; set; }

    public ProviderPayout? ProviderPayout { get; set; }
    public ICollection<ServiceOrderDispute> Disputes { get; set; } = new List<ServiceOrderDispute>();
}
