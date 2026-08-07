using ServiceMarketplace.Domain.Enums;

namespace ServiceMarketplace.Application.DTOs;

public class ProviderPayoutDto
{
    public Guid Id { get; set; }
    public Guid ServiceOrderPaymentId { get; set; }
    public Guid ServiceOrderId { get; set; }
    public string ProviderId { get; set; } = string.Empty;
    public decimal GrossAmount { get; set; }
    public decimal PlatformFeeAmount { get; set; }
    public decimal PayoutAmount { get; set; }
    public ProviderPayoutStatus Status { get; set; }
    public string? PayoutReference { get; set; }
    public string? Notes { get; set; }
    public string? MarkedPaidByUserId { get; set; }
    public DateTime? PaidAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
