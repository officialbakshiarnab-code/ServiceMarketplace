using Microsoft.EntityFrameworkCore;
using ServiceMarketplace.Application.DTOs;
using ServiceMarketplace.Application.Exceptions;
using ServiceMarketplace.Application.Interfaces;
using ServiceMarketplace.Domain.Enums;
using ServiceMarketplace.Infrastructure.Data;

namespace ServiceMarketplace.Infrastructure.Services;

public sealed class MarketplaceSearchService(AppDbContext context) : IMarketplaceSearchService
{
    private const int MaxTake = 60;

    public async Task<MarketplaceSearchResponse> SearchAsync(MarketplaceSearchRequest request)
    {
        var query = NormalizeQuery(request.Query);
        var take = request.Take <= 0 ? 30 : Math.Min(request.Take, MaxTake);
        ValidateRequest(request);

        var items = new List<MarketplaceSearchItemDto>();

        if (request.Type is null or MarketplaceSearchItemType.ServicePackage)
            items.AddRange(await SearchServicePackagesAsync(request, query));

        if (request.Type is null or MarketplaceSearchItemType.ProductListing)
            items.AddRange(await SearchProductListingsAsync(request, query));

        items = Sort(items, request.Sort)
            .Take(take)
            .ToList();

        return new MarketplaceSearchResponse
        {
            Query = query,
            TotalCount = items.Count,
            ServicePackageCount = items.Count(i => i.Type == MarketplaceSearchItemType.ServicePackage),
            ProductListingCount = items.Count(i => i.Type == MarketplaceSearchItemType.ProductListing),
            Items = items
        };
    }

    private async Task<List<MarketplaceSearchItemDto>> SearchServicePackagesAsync(MarketplaceSearchRequest request, string query)
    {
        var packages = context.ServicePackages
            .AsNoTracking()
            .Where(p => p.IsActive && p.ServiceCategory.IsActive);

        if (request.ServiceCategoryId.HasValue)
            packages = packages.Where(p => p.ServiceCategoryId == request.ServiceCategoryId.Value);

        if (request.ZoneId.HasValue)
            packages = packages.Where(p => !p.ServiceZoneId.HasValue || p.ServiceZoneId == request.ZoneId.Value);

        if (request.MinPrice.HasValue)
            packages = packages.Where(p => p.Price >= request.MinPrice.Value);

        if (request.MaxPrice.HasValue)
            packages = packages.Where(p => p.Price <= request.MaxPrice.Value);

        if (!string.IsNullOrWhiteSpace(query))
        {
            packages = packages.Where(p =>
                p.Title.ToLower().Contains(query) ||
                p.Description.ToLower().Contains(query) ||
                p.ServiceCategory.Name.ToLower().Contains(query));
        }

        var raw = await packages
            .Select(p => new
            {
                p.Id,
                p.ProviderId,
                p.Title,
                p.Description,
                p.Price,
                CategoryId = p.ServiceCategoryId,
                CategoryName = p.ServiceCategory.Name,
                ZoneId = p.ServiceZoneId,
                ZoneName = p.ServiceZone == null ? null : p.ServiceZone.DisplayName,
                p.EstimatedDurationMinutes,
                p.CreatedAt
            })
            .Take(MaxTake)
            .ToListAsync();

        var providerIds = raw
            .Select(p => Guid.TryParse(p.ProviderId, out var id) ? id : (Guid?)null)
            .Where(id => id.HasValue)
            .Select(id => id!.Value)
            .ToList();

        var profiles = await context.ServiceProviderProfiles
            .AsNoTracking()
            .Where(p => providerIds.Contains(p.UserId))
            .ToDictionaryAsync(p => p.UserId);

        return raw.Select(package =>
        {
            profiles.TryGetValue(Guid.TryParse(package.ProviderId, out var providerGuid) ? providerGuid : Guid.Empty, out var profile);

            return new MarketplaceSearchItemDto
            {
                Type = MarketplaceSearchItemType.ServicePackage,
                Id = package.Id,
                Title = package.Title,
                Description = package.Description,
                Price = package.Price,
                CategoryId = package.CategoryId,
                CategoryName = package.CategoryName,
                ZoneId = package.ZoneId,
                ZoneName = package.ZoneName,
                SellerOrProviderId = package.ProviderId,
                SellerOrProviderName = profile?.DisplayName ?? "Service Provider",
                BusinessName = profile?.BusinessName,
                EstimatedDurationMinutes = package.EstimatedDurationMinutes,
                ProviderAverageRating = profile?.AverageRating ?? 0m,
                ProviderReviewCount = profile?.ReviewCount ?? 0,
                RelevanceScore = Score(query, package.Title, package.Description, package.CategoryName, profile?.DisplayName, profile?.BusinessName),
                ActionLabel = "Book Package",
                ActionRoute = "/user/service-packages",
                CreatedAt = package.CreatedAt
            };
        }).ToList();
    }

    private async Task<List<MarketplaceSearchItemDto>> SearchProductListingsAsync(MarketplaceSearchRequest request, string query)
    {
        var products = context.ProductListings
            .AsNoTracking()
            .Where(p =>
                p.Status == ProductListingStatus.Active &&
                p.StockQuantity > 0 &&
                p.ProductCategory.IsActive &&
                p.SellerProfile.Status == SellerApplicationStatus.Approved);

        if (request.ProductCategoryId.HasValue)
            products = products.Where(p => p.ProductCategoryId == request.ProductCategoryId.Value);

        if (request.ZoneId.HasValue)
            products = products.Where(p => !p.ServiceZoneId.HasValue || p.ServiceZoneId == request.ZoneId.Value);

        if (request.ProductCondition.HasValue)
            products = products.Where(p => p.Condition == request.ProductCondition.Value);

        if (request.MinPrice.HasValue)
            products = products.Where(p => p.Price >= request.MinPrice.Value);

        if (request.MaxPrice.HasValue)
            products = products.Where(p => p.Price <= request.MaxPrice.Value);

        if (!string.IsNullOrWhiteSpace(query))
        {
            products = products.Where(p =>
                p.Title.ToLower().Contains(query) ||
                p.Description.ToLower().Contains(query) ||
                p.ProductCategory.Name.ToLower().Contains(query) ||
                p.SellerProfile.StoreName.ToLower().Contains(query));
        }

        var raw = await products
            .Select(p => new
            {
                p.Id,
                p.SellerId,
                p.Title,
                p.Description,
                p.Price,
                CategoryId = p.ProductCategoryId,
                CategoryName = p.ProductCategory.Name,
                ZoneId = p.ServiceZoneId,
                ZoneName = p.ServiceZone == null ? null : p.ServiceZone.DisplayName,
                p.ImageUrl,
                p.StockQuantity,
                p.Condition,
                p.CreatedAt,
                p.SellerProfile.StoreName,
                p.SellerProfile.BusinessName
            })
            .Take(MaxTake)
            .ToListAsync();

        return raw.Select(product => new MarketplaceSearchItemDto
        {
            Type = MarketplaceSearchItemType.ProductListing,
            Id = product.Id,
            Title = product.Title,
            Description = product.Description,
            Price = product.Price,
            CategoryId = product.CategoryId,
            CategoryName = product.CategoryName,
            ZoneId = product.ZoneId,
            ZoneName = product.ZoneName,
            SellerOrProviderId = product.SellerId.ToString(),
            SellerOrProviderName = product.StoreName,
            BusinessName = product.BusinessName,
            ImageUrl = product.ImageUrl,
            StockQuantity = product.StockQuantity,
            ProductCondition = product.Condition,
            ProductConditionLabel = ToConditionLabel(product.Condition),
            RelevanceScore = Score(query, product.Title, product.Description, product.CategoryName, product.StoreName, product.BusinessName),
            ActionLabel = "Order Delivery",
            ActionRoute = "/products",
            CreatedAt = product.CreatedAt
        }).ToList();
    }

    private static IEnumerable<MarketplaceSearchItemDto> Sort(List<MarketplaceSearchItemDto> items, MarketplaceSearchSort sort) => sort switch
    {
        MarketplaceSearchSort.PriceLowToHigh => items.OrderBy(i => i.Price).ThenByDescending(i => i.RelevanceScore),
        MarketplaceSearchSort.PriceHighToLow => items.OrderByDescending(i => i.Price).ThenByDescending(i => i.RelevanceScore),
        MarketplaceSearchSort.Newest => items.OrderByDescending(i => i.CreatedAt).ThenByDescending(i => i.RelevanceScore),
        _ => items.OrderByDescending(i => i.RelevanceScore).ThenBy(i => i.Price).ThenBy(i => i.Title)
    };

    private static int Score(string query, params string?[] fields)
    {
        if (string.IsNullOrWhiteSpace(query))
            return 1;

        var score = 0;
        foreach (var field in fields.Where(f => !string.IsNullOrWhiteSpace(f)))
        {
            var value = field!.ToLowerInvariant();
            if (value == query)
                score += 100;
            else if (value.StartsWith(query, StringComparison.Ordinal))
                score += 70;
            else if (value.Contains(query, StringComparison.Ordinal))
                score += 35;
        }

        return score;
    }

    private static void ValidateRequest(MarketplaceSearchRequest request)
    {
        if (request.Type.HasValue && !Enum.IsDefined(request.Type.Value))
            throw new BadRequestException("Selected marketplace result type is not valid.");

        if (request.Sort != MarketplaceSearchSort.Relevance && !Enum.IsDefined(request.Sort))
            throw new BadRequestException("Selected marketplace search sort is not valid.");

        if (request.ProductCondition.HasValue && !Enum.IsDefined(request.ProductCondition.Value))
            throw new BadRequestException("Selected product condition is not valid.");

        if (request.MinPrice < 0 || request.MaxPrice < 0)
            throw new BadRequestException("Price filters cannot be negative.");

        if (request.MinPrice.HasValue && request.MaxPrice.HasValue && request.MinPrice.Value > request.MaxPrice.Value)
            throw new BadRequestException("Minimum price cannot be greater than maximum price.");
    }

    private static string NormalizeQuery(string? query)
    {
        if (string.IsNullOrWhiteSpace(query))
            return string.Empty;

        query = query.Trim();
        return query.Length <= 100 ? query.ToLowerInvariant() : query[..100].ToLowerInvariant();
    }

    private static string ToConditionLabel(ProductCondition condition) => condition switch
    {
        ProductCondition.New => "New",
        ProductCondition.LikeNew => "Like New",
        ProductCondition.Good => "Good",
        ProductCondition.Fair => "Fair",
        ProductCondition.NeedsRepair => "Needs Repair",
        _ => condition.ToString()
    };
}
