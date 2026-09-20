using Microsoft.EntityFrameworkCore;
using ServiceMarketplace.Application.DTOs;
using ServiceMarketplace.Application.Exceptions;
using ServiceMarketplace.Application.Interfaces;
using ServiceMarketplace.Domain.Entities;
using ServiceMarketplace.Domain.Enums;
using ServiceMarketplace.Infrastructure.Data;

namespace ServiceMarketplace.Infrastructure.Services;

public sealed class ProductListingService(AppDbContext context) : IProductListingService
{
    public async Task<List<ProductListingDto>> GetActiveAsync(Guid? categoryId = null, Guid? zoneId = null, ProductCondition? condition = null)
    {
        if (condition.HasValue && !Enum.IsDefined(condition.Value))
            throw new BadRequestException("Selected product condition is not valid.");

        var query = context.ProductListings
            .AsNoTracking()
            .Where(p =>
                p.Status == ProductListingStatus.Active &&
                p.StockQuantity > 0 &&
                p.ProductCategory.IsActive &&
                p.SellerProfile.Status == SellerApplicationStatus.Approved);

        if (categoryId.HasValue)
            query = query.Where(p => p.ProductCategoryId == categoryId.Value);

        if (zoneId.HasValue)
            query = query.Where(p => !p.ServiceZoneId.HasValue || p.ServiceZoneId == zoneId.Value);

        if (condition.HasValue)
            query = query.Where(p => p.Condition == condition.Value);

        return await ToDtoListAsync(query
            .OrderBy(p => p.Price)
            .ThenBy(p => p.Title));
    }

    public async Task<List<ProductListingDto>> GetMineAsync(string sellerId)
    {
        if (!Guid.TryParse(sellerId, out var sellerGuid))
            return [];

        return await ToDtoListAsync(context.ProductListings
            .AsNoTracking()
            .Where(p => p.SellerId == sellerGuid)
            .OrderByDescending(p => p.CreatedAt));
    }

    public async Task<ProductListingDto> CreateAsync(string sellerId, UpsertProductListingDto dto)
    {
        var sellerProfile = await EnsureApprovedSellerAsync(sellerId);
        await ValidateListingAsync(dto);

        var listing = new ProductListing
        {
            SellerId = sellerProfile.UserId,
            ProductCategoryId = dto.ProductCategoryId,
            ServiceZoneId = dto.ServiceZoneId,
            Title = NormalizeRequired(dto.Title, 150, "Product title"),
            Description = NormalizeRequired(dto.Description, 1000, "Product description"),
            Price = dto.Price,
            StockQuantity = dto.StockQuantity,
            ImageUrl = NormalizeOptional(dto.ImageUrl, 500),
            Condition = dto.Condition,
            ConditionNotes = NormalizeOptional(dto.ConditionNotes, 1000),
            InspectionChecklist = NormalizeOptional(dto.InspectionChecklist, 2000),
            PurchaseYear = dto.PurchaseYear,
            HasOriginalBill = dto.HasOriginalBill,
            HasWarranty = dto.HasWarranty,
            Status = dto.IsActive ? ProductListingStatus.Active : ProductListingStatus.Inactive,
            CreatedAt = DateTime.UtcNow
        };

        context.ProductListings.Add(listing);
        await context.SaveChangesAsync();

        return await GetDtoByIdAsync(listing.Id);
    }

    public async Task<ProductListingDto> UpdateAsync(Guid listingId, string sellerId, UpsertProductListingDto dto)
    {
        return await context.ExecuteAtomicAsync(async () =>
        {
            var sellerProfile = await EnsureApprovedSellerAsync(sellerId);
            await ValidateListingAsync(dto);

            var listing = await context.LockProductListing(listingId)
                .FirstOrDefaultAsync(p => p.Id == listingId && p.SellerId == sellerProfile.UserId)
                ?? throw new NotFoundException("Product listing not found.");

            listing.ProductCategoryId = dto.ProductCategoryId;
            listing.ServiceZoneId = dto.ServiceZoneId;
            listing.Title = NormalizeRequired(dto.Title, 150, "Product title");
            listing.Description = NormalizeRequired(dto.Description, 1000, "Product description");
            listing.Price = dto.Price;
            listing.StockQuantity = dto.StockQuantity;
            listing.ImageUrl = NormalizeOptional(dto.ImageUrl, 500);
            listing.Condition = dto.Condition;
            listing.ConditionNotes = NormalizeOptional(dto.ConditionNotes, 1000);
            listing.InspectionChecklist = NormalizeOptional(dto.InspectionChecklist, 2000);
            listing.PurchaseYear = dto.PurchaseYear;
            listing.HasOriginalBill = dto.HasOriginalBill;
            listing.HasWarranty = dto.HasWarranty;
            listing.Status = dto.IsActive ? ProductListingStatus.Active : ProductListingStatus.Inactive;
            listing.UpdatedAt = DateTime.UtcNow;

            await context.SaveChangesAsync();

            return await GetDtoByIdAsync(listing.Id);
        });
    }

    private async Task<ProductListingDto> GetDtoByIdAsync(Guid listingId)
    {
        var listings = await ToDtoListAsync(context.ProductListings
            .AsNoTracking()
            .Where(p => p.Id == listingId));

        return listings.Single();
    }

    private async Task<SellerProfile> EnsureApprovedSellerAsync(string sellerId)
    {
        if (!Guid.TryParse(sellerId, out var sellerGuid))
            throw new ForbiddenException("Seller approval is required before managing products.");

        var profile = await context.SellerProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.UserId == sellerGuid && s.Status == SellerApplicationStatus.Approved);

        if (profile == null)
            throw new ForbiddenException("Seller approval is required before managing products.");

        return profile;
    }

    private async Task ValidateListingAsync(UpsertProductListingDto dto)
    {
        _ = NormalizeRequired(dto.Title, 150, "Product title");
        _ = NormalizeRequired(dto.Description, 1000, "Product description");

        if (dto.Price <= 0)
            throw new BadRequestException("Product price must be greater than zero.");

        if (dto.StockQuantity < 0)
            throw new BadRequestException("Stock quantity cannot be negative.");

        if (dto.IsActive && dto.StockQuantity <= 0)
            throw new BadRequestException("Active product listings must have stock available.");

        if (!Enum.IsDefined(dto.Condition))
            throw new BadRequestException("Selected product condition is not valid.");

        if (dto.Condition == ProductCondition.New)
        {
            if (!string.IsNullOrWhiteSpace(dto.ConditionNotes) || !string.IsNullOrWhiteSpace(dto.InspectionChecklist) ||
                dto.PurchaseYear.HasValue || dto.HasOriginalBill || dto.HasWarranty)
            {
                throw new BadRequestException("Condition details are only allowed for used products.");
            }
        }
        else
        {
            _ = NormalizeRequired(dto.ConditionNotes, 1000, "Used product condition notes");
            _ = NormalizeRequired(dto.InspectionChecklist, 2000, "Used product inspection checklist");

            if (dto.PurchaseYear.HasValue)
            {
                var currentYear = DateTime.UtcNow.Year;
                if (dto.PurchaseYear.Value < 1970 || dto.PurchaseYear.Value > currentYear)
                    throw new BadRequestException($"Purchase year must be between 1970 and {currentYear}.");
            }
        }

        var categoryExists = await context.ProductCategories
            .AsNoTracking()
            .AnyAsync(c => c.Id == dto.ProductCategoryId && c.IsActive);

        if (!categoryExists)
            throw new BadRequestException("Selected product category is not available.");

        if (dto.ServiceZoneId.HasValue)
        {
            var zoneExists = await context.ServiceZones
                .AsNoTracking()
                .AnyAsync(z => z.Id == dto.ServiceZoneId.Value && z.IsActive);

            if (!zoneExists)
                throw new BadRequestException("Selected product zone is not available.");
        }
    }

    private async Task<List<ProductListingDto>> ToDtoListAsync(IQueryable<ProductListing> query)
    {
        var listings = await query
            .Select(listing => new ProductListingDto
            {
                Id = listing.Id,
                SellerId = listing.SellerId.ToString(),
                StoreName = listing.SellerProfile.StoreName,
                SellerBusinessName = listing.SellerProfile.BusinessName,
                ProductCategoryId = listing.ProductCategoryId,
                ProductCategoryName = listing.ProductCategory.Name,
                ServiceZoneId = listing.ServiceZoneId,
                ServiceZoneName = listing.ServiceZone == null ? null : listing.ServiceZone.DisplayName,
                Title = listing.Title,
                Description = listing.Description,
                Price = listing.Price,
                StockQuantity = listing.StockQuantity,
                ImageUrl = listing.ImageUrl,
                Condition = listing.Condition,
                IsUsed = listing.Condition != ProductCondition.New,
                ConditionNotes = listing.ConditionNotes,
                InspectionChecklist = listing.InspectionChecklist,
                PurchaseYear = listing.PurchaseYear,
                HasOriginalBill = listing.HasOriginalBill,
                HasWarranty = listing.HasWarranty,
                Status = listing.Status,
                IsActive = listing.Status == ProductListingStatus.Active,
                CreatedAt = listing.CreatedAt,
                UpdatedAt = listing.UpdatedAt
            })
            .ToListAsync();

        foreach (var listing in listings)
            listing.ConditionLabel = ToConditionLabel(listing.Condition);

        await AttachInspectionPromptsAsync(listings);
        return listings;
    }

    private async Task AttachInspectionPromptsAsync(List<ProductListingDto> listings)
    {
        var usedCategoryIds = listings
            .Where(l => l.IsUsed)
            .Select(l => l.ProductCategoryId)
            .Distinct()
            .ToList();

        if (usedCategoryIds.Count == 0)
            return;

        var prompts = await context.ProductInspectionPrompts
            .AsNoTracking()
            .Where(p => p.IsActive && usedCategoryIds.Contains(p.ProductCategoryId))
            .OrderBy(p => p.ProductCategoryId)
            .ThenBy(p => p.SortOrder)
            .ThenBy(p => p.Prompt)
            .Select(p => new { p.ProductCategoryId, p.Prompt })
            .ToListAsync();

        var promptLookup = prompts
            .GroupBy(p => p.ProductCategoryId)
            .ToDictionary(g => g.Key, g => g.Select(p => p.Prompt).ToList());

        foreach (var listing in listings.Where(l => l.IsUsed))
        {
            if (promptLookup.TryGetValue(listing.ProductCategoryId, out var listingPrompts))
                listing.UsedInspectionPrompts = listingPrompts;
        }
    }

    private static string NormalizeRequired(string? value, int maxLength, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new BadRequestException($"{fieldName} is required.");

        value = value.Trim();
        if (value.Length > maxLength)
            throw new BadRequestException($"{fieldName} cannot exceed {maxLength} characters.");

        return value;
    }

    private static string? NormalizeOptional(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        value = value.Trim();
        return value.Length <= maxLength ? value : value[..maxLength];
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
