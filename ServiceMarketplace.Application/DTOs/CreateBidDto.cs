namespace ServiceMarketplace.Application.DTOs;

// Purpose: Input payload when a service provider places a bid.
public class CreateBidDto
{
    public Guid ServiceRequestId { get; set; }
    public decimal Amount { get; set; }
    public DateTime ProposedDateTime { get; set; }
    public int? EstimatedDurationMinutes { get; set; }
    public string? Message { get; set; }
}
