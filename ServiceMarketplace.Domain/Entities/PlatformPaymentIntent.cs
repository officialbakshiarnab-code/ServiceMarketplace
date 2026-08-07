using ServiceMarketplace.Domain.Enums;

namespace ServiceMarketplace.Domain.Entities;

public class PlatformPaymentIntent : BaseAuditableEntity
{
    public Guid ServiceOrderId { get; set; }
    public ServiceOrder ServiceOrder { get; set; } = null!;

    public string CustomerId { get; set; } = string.Empty;
    public string ProviderId { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public decimal PlatformFeeAmount { get; set; }
    public decimal ProviderPayoutAmount { get; set; }
    public PlatformPaymentIntentStatus Status { get; set; } = PlatformPaymentIntentStatus.PendingVerification;
    public string GatewayReference { get; set; } = string.Empty;
    public string? GatewayPaymentId { get; set; }
    public string? VerificationNotes { get; set; }
    public string? FailureReason { get; set; }
    public string? VerifiedByUserId { get; set; }
    public DateTime ExpiresAt { get; set; }
    public DateTime? VerifiedAt { get; set; }
}
