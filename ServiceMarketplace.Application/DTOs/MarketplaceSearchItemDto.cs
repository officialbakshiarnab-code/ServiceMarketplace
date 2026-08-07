using ServiceMarketplace.Domain.Enums;

namespace ServiceMarketplace.Application.DTOs;

public class MarketplaceSearchItemDto
{
    public MarketplaceSearchItemType Type { get; set; }
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public Guid CategoryId { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public Guid? ZoneId { get; set; }
    public string? ZoneName { get; set; }
    public string SellerOrProviderId { get; set; } = string.Empty;
    public string SellerOrProviderName { get; set; } = string.Empty;
    public string? BusinessName { get; set; }
    public string? ImageUrl { get; set; }
    public int? StockQuantity { get; set; }
    public ProductCondition? ProductCondition { get; set; }
    public string? ProductConditionLabel { get; set; }
    public int? EstimatedDurationMinutes { get; set; }
    public decimal? ProviderAverageRating { get; set; }
    public int? ProviderReviewCount { get; set; }
    public int RelevanceScore { get; set; }
    public string ActionLabel { get; set; } = string.Empty;
    public string ActionRoute { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}
