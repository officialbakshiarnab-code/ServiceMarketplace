using Microsoft.EntityFrameworkCore;
using ServiceMarketplace.Application.DTOs;
using ServiceMarketplace.Application.Interfaces;
using ServiceMarketplace.Infrastructure.Data;

namespace ServiceMarketplace.Infrastructure.Services;

public sealed class ProductCatalogService(AppDbContext context) : IProductCatalogService
{
    public async Task<IReadOnlyList<ProductCategoryDto>> GetCategoriesAsync()
    {
        var categories = await context.ProductCategories
            .AsNoTracking()
            .Where(c => c.IsActive)
            .OrderBy(c => c.SortOrder)
            .ThenBy(c => c.Name)
            .Select(c => new ProductCategoryDto
            {
                Id = c.Id,
                Name = c.Name,
                Slug = c.Slug,
                Description = c.Description,
                SortOrder = c.SortOrder,
                UsedInspectionPrompts = new List<string>()
            })
            .ToListAsync();

        if (categories.Count == 0)
            return categories;

        var categoryIds = categories.Select(c => c.Id).ToList();
        var prompts = await context.ProductInspectionPrompts
            .AsNoTracking()
            .Where(p => p.IsActive && categoryIds.Contains(p.ProductCategoryId))
            .OrderBy(p => p.ProductCategoryId)
            .ThenBy(p => p.SortOrder)
            .ThenBy(p => p.Prompt)
            .Select(p => new { p.ProductCategoryId, p.Prompt })
            .ToListAsync();

        var promptLookup = prompts
            .GroupBy(p => p.ProductCategoryId)
            .ToDictionary(g => g.Key, g => g.Select(p => p.Prompt).ToList());

        foreach (var category in categories)
        {
            if (promptLookup.TryGetValue(category.Id, out var categoryPrompts))
                category.UsedInspectionPrompts = categoryPrompts;
        }

        return categories;
    }
}
