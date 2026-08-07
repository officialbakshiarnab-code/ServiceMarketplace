using ServiceMarketplace.Domain.Enums;

namespace ServiceMarketplace.Application.DTOs;

public class MarketplaceSearchRequest
{
    public string? Query { get; set; }
    public MarketplaceSearchItemType? Type { get; set; }
    public Guid? ServiceCategoryId { get; set; }
    public Guid? ProductCategoryId { get; set; }
    public Guid? ZoneId { get; set; }
    public ProductCondition? ProductCondition { get; set; }
    public decimal? MinPrice { get; set; }
    public decimal? MaxPrice { get; set; }
    public MarketplaceSearchSort Sort { get; set; } = MarketplaceSearchSort.Relevance;
    public int Take { get; set; } = 30;
}
