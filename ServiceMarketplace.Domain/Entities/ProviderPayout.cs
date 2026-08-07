using ServiceMarketplace.Domain.Enums;

namespace ServiceMarketplace.Domain.Entities;

public class ProviderPayout : BaseAuditableEntity
{
    public Guid ServiceOrderPaymentId { get; set; }
    public ServiceOrderPayment ServiceOrderPayment { get; set; } = null!;

    public Guid ServiceOrderId { get; set; }
    public ServiceOrder ServiceOrder { get; set; } = null!;

    public string ProviderId { get; set; } = string.Empty;
    public decimal GrossAmount { get; set; }
    public decimal PlatformFeeAmount { get; set; }
    public decimal PayoutAmount { get; set; }
    public ProviderPayoutStatus Status { get; set; } = ProviderPayoutStatus.Pending;
    public string? PayoutReference { get; set; }
    public string? Notes { get; set; }
    public string? MarkedPaidByUserId { get; set; }
    public DateTime? PaidAt { get; set; }
}
