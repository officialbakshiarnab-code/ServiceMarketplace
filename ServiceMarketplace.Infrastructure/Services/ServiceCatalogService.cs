using Microsoft.EntityFrameworkCore;
using ServiceMarketplace.Application.DTOs;
using ServiceMarketplace.Application.Interfaces;
using ServiceMarketplace.Infrastructure.Data;

namespace ServiceMarketplace.Infrastructure.Services;

public sealed class ServiceCatalogService(AppDbContext context) : IServiceCatalogService
{
    public async Task<IReadOnlyList<ServiceCategoryDto>> GetCategoriesAsync(bool activeOnly = true)
    {
        var query = context.ServiceCategories.AsNoTracking();
        if (activeOnly)
            query = query.Where(c => c.IsActive);

        return await query
            .OrderBy(c => c.SortOrder)
            .ThenBy(c => c.Name)
            .Select(c => new ServiceCategoryDto
            {
                Id = c.Id,
                Name = c.Name,
                Slug = c.Slug,
                Description = c.Description,
                IsActive = c.IsActive,
                SortOrder = c.SortOrder
            })
            .ToListAsync();
    }

    public async Task<IReadOnlyList<ServiceZoneDto>> GetZonesAsync(bool activeOnly = true)
    {
        var query = context.ServiceZones.AsNoTracking();
        if (activeOnly)
            query = query.Where(z => z.IsActive);

        return await query
            .OrderBy(z => z.SortOrder)
            .ThenBy(z => z.DisplayName)
            .Select(z => new ServiceZoneDto
            {
                Id = z.Id,
                Country = z.Country,
                State = z.State,
                City = z.City,
                ZoneName = z.ZoneName,
                DisplayName = z.DisplayName,
                PinCodeRegion = z.PinCodeRegion,
                IsActive = z.IsActive,
                SortOrder = z.SortOrder
            })
            .ToListAsync();
    }
}
