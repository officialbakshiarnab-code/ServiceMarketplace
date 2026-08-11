using ServiceMarketplace.Domain.Enums;

namespace ServiceMarketplace.Application.DTOs;

// Purpose: Bid details returned to clients.
public class BidDto
{
    public Guid Id { get; set; }
    public Guid ServiceRequestId { get; set; }
    public string ServiceProviderId { get; set; } = string.Empty;
    public string ProviderDisplayName { get; set; } = string.Empty;
    public string? ProviderBusinessName { get; set; }
    public decimal? ProviderHourlyRate { get; set; }
    public decimal ProviderAverageRating { get; set; }
    public int ProviderReviewCount { get; set; }
    public decimal Amount { get; set; }
    public DateTime ProposedDateTime { get; set; }
    public int? EstimatedDurationMinutes { get; set; }
    public string? Message { get; set; }
    public BidStatus Status { get; set; }
    public int ComparisonRank { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
