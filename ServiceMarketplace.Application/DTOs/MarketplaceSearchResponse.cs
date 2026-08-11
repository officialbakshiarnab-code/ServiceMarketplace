namespace ServiceMarketplace.Application.DTOs;

public class MarketplaceSearchResponse
{
    public string Query { get; set; } = string.Empty;
    public int TotalCount { get; set; }
    public int ServicePackageCount { get; set; }
    public int ProductListingCount { get; set; }
    public List<MarketplaceSearchItemDto> Items { get; set; } = [];
}
