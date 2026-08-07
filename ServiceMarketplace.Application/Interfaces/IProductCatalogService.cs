using ServiceMarketplace.Application.DTOs;

namespace ServiceMarketplace.Application.Interfaces;

public interface IProductCatalogService
{
    Task<IReadOnlyList<ProductCategoryDto>> GetCategoriesAsync();
}
