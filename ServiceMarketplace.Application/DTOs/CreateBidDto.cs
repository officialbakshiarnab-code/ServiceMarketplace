namespace ServiceMarketplace.Application.DTOs;

public class CreateBidDto
{
    public Guid ServiceRequestId { get; set; }
    public decimal Amount { get; set; }
    public DateTime ProposedDateTime { get; set; }
}
