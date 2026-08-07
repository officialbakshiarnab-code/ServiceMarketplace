using System.Text.Json.Serialization;
using ServiceMarketplace.Domain.Enums;

namespace ServiceMarketplace.Application.DTOs;

// Purpose: Bid summary for service providers viewing their own bids.
public class ProviderBidDto
{
    public Guid Id { get; set; }
    public Guid ServiceRequestId { get; set; }
    public string RequestTitle { get; set; } = string.Empty;
    public string RequestCategory { get; set; } = string.Empty;
    public string ApproximateLocation { get; set; } = string.Empty;
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ExactLocation { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public double? Latitude { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public double? Longitude { get; set; }
    public bool IsExactLocationVisible { get; set; }
    public decimal Amount { get; set; }
    public DateTime ProposedDateTime { get; set; }
    public int? EstimatedDurationMinutes { get; set; }
    public string? Message { get; set; }
    public BidStatus Status { get; set; }
    public DateTime CreatedAt { get; set; }
}
