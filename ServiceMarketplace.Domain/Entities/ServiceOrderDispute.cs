using ServiceMarketplace.Domain.Enums;

namespace ServiceMarketplace.Domain.Entities;

public class ServiceOrderDispute : BaseAuditableEntity
{
    public Guid ServiceOrderId { get; set; }
    public ServiceOrder ServiceOrder { get; set; } = null!;

    public Guid? ServiceOrderPaymentId { get; set; }
    public ServiceOrderPayment? ServiceOrderPayment { get; set; }

    public string RaisedByUserId { get; set; } = string.Empty;
    public string AgainstUserId { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public ServiceOrderDisputeStatus Status { get; set; } = ServiceOrderDisputeStatus.Open;
    public string? ResolutionNotes { get; set; }
    public string? ResolvedByUserId { get; set; }
    public DateTime? ResolvedAt { get; set; }
}
