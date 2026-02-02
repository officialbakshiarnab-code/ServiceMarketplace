using ServiceMarketplace.Domain.Enums;

namespace ServiceMarketplace.Application.DTOs;

// Purpose: Bid summary for service providers viewing their own bids.
public class ProviderBidDto
{
    public Guid Id { get; set; }
    public Guid ServiceRequestId { get; set; }
    public string RequestTitle { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public DateTime ProposedDateTime { get; set; }
    public string? Message { get; set; }
    public BidStatus Status { get; set; }
    public DateTime CreatedAt { get; set; }
}
