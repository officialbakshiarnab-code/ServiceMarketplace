using Microsoft.EntityFrameworkCore;
using ServiceMarketplace.Application.DTOs;
using ServiceMarketplace.Application.Exceptions;
using ServiceMarketplace.Application.Interfaces;
using ServiceMarketplace.Domain.Enums;
using ServiceMarketplace.Infrastructure.Data;

namespace ServiceMarketplace.Infrastructure.Services;

public sealed class ProfileDirectoryService(AppDbContext context) : IProfileDirectoryService
{
    public async Task<ProfileSearchResponse> SearchAsync(ProfileSearchRequest request)
    {
        var take = Math.Clamp(request.Take, 1, 50);
        var query = (request.Query ?? string.Empty).Trim().ToLowerInvariant();
        var results = new List<ProfileSearchItemDto>();

        if (request.ProfileType is null or ContactProfileType.Provider)
            results.AddRange(await SearchProvidersAsync(query, request.ServiceCategoryId, request.ZoneId, take));

        if (request.ProfileType is null or ContactProfileType.Seller)
            results.AddRange(await SearchSellersAsync(query, request.ZoneId, take));

        if (request.ProfileType is null or ContactProfileType.User)
            results.AddRange(await SearchUsersAsync(query, take));

        var ordered = results
            .OrderByDescending(i => i.IsAvailable)
            .ThenBy(i => i.ProfileType)
            .ThenBy(i => i.DisplayName)
            .Take(take)
            .ToList();

        return new ProfileSearchResponse
        {
            TotalCount = results.Count,
            Items = ordered
        };
    }

    public async Task<ProfileDetailDto> GetDetailAsync(ContactProfileType profileType, string userId)
    {
        if (!Guid.TryParse(userId, out var parsedUserId))
            throw new BadRequestException("Invalid user id.");

        return profileType switch
        {
            ContactProfileType.Provider => await GetProviderDetailAsync(parsedUserId),
            ContactProfileType.Seller => await GetSellerDetailAsync(parsedUserId),
            ContactProfileType.User => await GetUserDetailAsync(parsedUserId),
            _ => throw new BadRequestException("Unsupported profile type.")
        };
    }

    private async Task<List<ProfileSearchItemDto>> SearchProvidersAsync(string query, Guid? categoryId, Guid? zoneId, int take)
    {
        var providers = context.ServiceProviderProfiles
            .AsNoTracking()
            .Include(p => p.User)
            .Include(p => p.ServiceCategory)
            .Include(p => p.ServiceZone)
            .Where(p => p.User.IsActive && p.Status == ProviderApplicationStatus.Approved);

        if (categoryId.HasValue)
            providers = providers.Where(p => p.ServiceCategoryId == categoryId.Value);

        if (zoneId.HasValue)
            providers = providers.Where(p => p.ServiceZoneId == zoneId.Value);

        if (!string.IsNullOrWhiteSpace(query))
        {
            providers = providers.Where(p =>
                p.DisplayName.ToLower().Contains(query) ||
                (p.BusinessName != null && p.BusinessName.ToLower().Contains(query)) ||
                p.PrimaryCategory.ToLower().Contains(query) ||
                p.User.FirstName.ToLower().Contains(query) ||
                p.User.LastName.ToLower().Contains(query) ||
                (p.User.Email != null && p.User.Email.ToLower().Contains(query)));
        }

        return await providers
            .OrderByDescending(p => p.IsAvailable)
            .ThenByDescending(p => p.AverageRating)
            .Take(take)
            .Select(p => new ProfileSearchItemDto
            {
                ProfileType = ContactProfileType.Provider,
                UserId = p.UserId.ToString(),
                DisplayName = p.DisplayName,
                BusinessName = p.BusinessName,
                Summary = p.Bio,
                ServiceCategoryId = p.ServiceCategoryId,
                ServiceCategoryName = p.ServiceCategory != null ? p.ServiceCategory.Name : p.PrimaryCategory,
                ZoneId = p.ServiceZoneId,
                ZoneName = p.ServiceZone != null ? p.ServiceZone.DisplayName : p.ServiceAreaZone,
                AverageRating = p.AverageRating,
                ReviewCount = p.ReviewCount,
                IsAvailable = p.IsAvailable,
                ActiveOfferCount = context.ServicePackages.Count(sp => sp.ProviderId == p.UserId.ToString() && sp.IsActive)
            })
            .ToListAsync();
    }

    private async Task<List<ProfileSearchItemDto>> SearchSellersAsync(string query, Guid? zoneId, int take)
    {
        var sellers = context.SellerProfiles
            .AsNoTracking()
            .Include(s => s.User)
            .Include(s => s.ServiceZone)
            .Where(s => s.User.IsActive && s.Status == SellerApplicationStatus.Approved);

        if (zoneId.HasValue)
            sellers = sellers.Where(s => s.ServiceZoneId == zoneId.Value);

        if (!string.IsNullOrWhiteSpace(query))
        {
            sellers = sellers.Where(s =>
                s.StoreName.ToLower().Contains(query) ||
                (s.BusinessName != null && s.BusinessName.ToLower().Contains(query)) ||
                s.User.FirstName.ToLower().Contains(query) ||
                s.User.LastName.ToLower().Contains(query) ||
                (s.User.Email != null && s.User.Email.ToLower().Contains(query)));
        }

        return await sellers
            .OrderBy(s => s.StoreName)
            .Take(take)
            .Select(s => new ProfileSearchItemDto
            {
                ProfileType = ContactProfileType.Seller,
                UserId = s.UserId.ToString(),
                DisplayName = s.StoreName,
                BusinessName = s.BusinessName,
                Summary = s.Description,
                ZoneId = s.ServiceZoneId,
                ZoneName = s.ServiceZone != null ? s.ServiceZone.DisplayName : s.City,
                IsAvailable = true,
                ActiveOfferCount = s.ProductListings.Count(p => p.Status == ProductListingStatus.Active)
            })
            .ToListAsync();
    }

    private async Task<List<ProfileSearchItemDto>> SearchUsersAsync(string query, int take)
    {
        var providerUserIds = context.ServiceProviderProfiles
            .Where(p => p.Status == ProviderApplicationStatus.Approved)
            .Select(p => p.UserId);

        var sellerUserIds = context.SellerProfiles
            .Where(s => s.Status == SellerApplicationStatus.Approved)
            .Select(s => s.UserId);

        var users = context.Users
            .AsNoTracking()
            .Where(u => u.IsActive &&
                !providerUserIds.Contains(u.Id) &&
                !sellerUserIds.Contains(u.Id));

        if (!string.IsNullOrWhiteSpace(query))
        {
            users = users.Where(u =>
                u.FirstName.ToLower().Contains(query) ||
                u.LastName.ToLower().Contains(query) ||
                (u.Email != null && u.Email.ToLower().Contains(query)));
        }

        return await users
            .OrderBy(u => u.FirstName)
            .ThenBy(u => u.LastName)
            .Take(take)
            .Select(u => new ProfileSearchItemDto
            {
                ProfileType = ContactProfileType.User,
                UserId = u.Id.ToString(),
                DisplayName = (u.FirstName + " " + u.LastName).Trim(),
                Summary = "Marketplace customer",
                IsAvailable = true
            })
            .ToListAsync();
    }

    private async Task<ProfileDetailDto> GetProviderDetailAsync(Guid userId)
    {
        var profile = await context.ServiceProviderProfiles
            .AsNoTracking()
            .Include(p => p.User)
            .Include(p => p.ServiceCategory)
            .Include(p => p.ServiceZone)
            .FirstOrDefaultAsync(p => p.UserId == userId && p.User.IsActive && p.Status == ProviderApplicationStatus.Approved)
            ?? throw new NotFoundException("Provider profile not found.");

        var packageCount = await context.ServicePackages
            .CountAsync(p => p.ProviderId == userId.ToString() && p.IsActive);

        return new ProfileDetailDto
        {
            ProfileType = ContactProfileType.Provider,
            UserId = userId.ToString(),
            DisplayName = profile.DisplayName,
            BusinessName = profile.BusinessName,
            Summary = profile.Bio,
            Description = profile.Skills,
            ServiceCategoryId = profile.ServiceCategoryId,
            ServiceCategoryName = profile.ServiceCategory?.Name ?? profile.PrimaryCategory,
            ZoneId = profile.ServiceZoneId,
            ZoneName = profile.ServiceZone?.DisplayName ?? profile.ServiceAreaZone,
            AverageRating = profile.AverageRating,
            ReviewCount = profile.ReviewCount,
            IsAvailable = profile.IsAvailable,
            ActiveOfferCount = packageCount,
            CreatedAt = profile.CreatedAt,
            Highlights = [profile.PrimaryCategory, $"{packageCount} active package(s)", profile.IsAvailable ? "Available" : "Currently unavailable"]
        };
    }

    private async Task<ProfileDetailDto> GetSellerDetailAsync(Guid userId)
    {
        var profile = await context.SellerProfiles
            .AsNoTracking()
            .Include(s => s.User)
            .Include(s => s.ServiceZone)
            .FirstOrDefaultAsync(s => s.UserId == userId && s.User.IsActive && s.Status == SellerApplicationStatus.Approved)
            ?? throw new NotFoundException("Seller profile not found.");

        var productCount = await context.ProductListings
            .CountAsync(p => p.SellerId == profile.UserId && p.Status == ProductListingStatus.Active);

        return new ProfileDetailDto
        {
            ProfileType = ContactProfileType.Seller,
            UserId = userId.ToString(),
            DisplayName = profile.StoreName,
            BusinessName = profile.BusinessName,
            Summary = profile.Description,
            Description = profile.Description,
            ZoneId = profile.ServiceZoneId,
            ZoneName = profile.ServiceZone?.DisplayName ?? profile.City,
            IsAvailable = true,
            ActiveOfferCount = productCount,
            CreatedAt = profile.CreatedAt,
            Highlights = [$"{productCount} active product(s)", profile.City, "Seller profile verified"]
        };
    }

    private async Task<ProfileDetailDto> GetUserDetailAsync(Guid userId)
    {
        var hasCommercialProfile = await HasApprovedCommercialProfileAsync(userId);
        if (hasCommercialProfile)
            throw new NotFoundException("User profile not found.");

        var user = await context.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == userId && u.IsActive)
            ?? throw new NotFoundException("User profile not found.");

        return new ProfileDetailDto
        {
            ProfileType = ContactProfileType.User,
            UserId = user.Id.ToString(),
            DisplayName = (user.FirstName + " " + user.LastName).Trim(),
            Summary = "Marketplace customer",
            Description = "Public customer profile",
            IsAvailable = true,
            CreatedAt = user.CreatedDate,
            Highlights = ["Customer account", "Contact details hidden until approved"]
        };
    }

    private async Task<bool> HasApprovedCommercialProfileAsync(Guid userId)
    {
        return await context.ServiceProviderProfiles.AnyAsync(p =>
                p.UserId == userId && p.Status == ProviderApplicationStatus.Approved) ||
            await context.SellerProfiles.AnyAsync(s =>
                s.UserId == userId && s.Status == SellerApplicationStatus.Approved);
    }
}
