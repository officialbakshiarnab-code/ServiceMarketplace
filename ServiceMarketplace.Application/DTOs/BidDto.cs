using ServiceMarketplace.Domain.Enums;

namespace ServiceMarketplace.Application.DTOs;

// Purpose: Bid details returned to clients.
public class BidDto
{
    public Guid Id { get; set; }
    public Guid ServiceRequestId { get; set; }
    public string ServiceProviderId { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public DateTime ProposedDateTime { get; set; }
    public BidStatus Status { get; set; }
}
