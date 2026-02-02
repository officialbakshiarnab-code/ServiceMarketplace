using ServiceMarketplace.Domain.Enums;

namespace ServiceMarketplace.Domain.Entities;

public class Bid : BaseAuditableEntity
{
    public Guid ServiceRequestId { get; set; }

    public string ServiceProviderId { get; set; } = null!;

    public decimal Amount { get; set; }
    public DateTime ProposedDateTime { get; set; }
    public string? Message { get; set; }

    public BidStatus Status { get; set; } = BidStatus.Pending;

    public ServiceRequest ServiceRequest { get; set; } = null!;
}

