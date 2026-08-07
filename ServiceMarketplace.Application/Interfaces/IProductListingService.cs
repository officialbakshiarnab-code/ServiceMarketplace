using ServiceMarketplace.Application.DTOs;
using ServiceMarketplace.Domain.Enums;

namespace ServiceMarketplace.Application.Interfaces;

public interface IProductListingService
{
    Task<List<ProductListingDto>> GetActiveAsync(Guid? categoryId = null, Guid? zoneId = null, ProductCondition? condition = null);
    Task<List<ProductListingDto>> GetMineAsync(string sellerId);
    Task<ProductListingDto> CreateAsync(string sellerId, UpsertProductListingDto dto);
    Task<ProductListingDto> UpdateAsync(Guid listingId, string sellerId, UpsertProductListingDto dto);
}
