using ServiceMarketplace.Domain.Enums;

namespace ServiceMarketplace.Domain.Entities;

public class ServiceOrderPayment : BaseAuditableEntity
{
    public Guid ServiceOrderId { get; set; }
    public ServiceOrder ServiceOrder { get; set; } = null!;

    public string CustomerId { get; set; } = string.Empty;
    public string ProviderId { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public PaymentMethod Method { get; set; }
    public PaymentStatus Status { get; set; } = PaymentStatus.Pending;
    public string? ReferenceNumber { get; set; }
    public string? Notes { get; set; }
    public string RecordedByUserId { get; set; } = string.Empty;
    public DateTime RecordedAt { get; set; }
    public DateTime? ReleasedAt { get; set; }
}
