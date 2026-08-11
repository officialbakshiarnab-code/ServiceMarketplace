using ServiceMarketplace.Application.DTOs;

namespace ServiceMarketplace.Application.Interfaces;

public interface IMarketplaceSearchService
{
    Task<MarketplaceSearchResponse> SearchAsync(MarketplaceSearchRequest request);
}
