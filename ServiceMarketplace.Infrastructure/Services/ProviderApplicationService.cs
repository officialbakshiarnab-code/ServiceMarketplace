using Microsoft.EntityFrameworkCore;
using ServiceMarketplace.Application.Constants;
using ServiceMarketplace.Application.DTOs;
using ServiceMarketplace.Application.Exceptions;
using ServiceMarketplace.Application.Interfaces;
using ServiceMarketplace.Domain.Entities;
using ServiceMarketplace.Domain.Enums;
using ServiceMarketplace.Infrastructure.Data;
using ServiceMarketplace.Infrastructure.Data.Extensions;

namespace ServiceMarketplace.Infrastructure.Services;

public sealed class ProviderApplicationService(AppDbContext context) : IProviderApplicationService
{
    private static readonly ProviderApplicationStatus[] EditableStatuses =
    [
        ProviderApplicationStatus.Draft,
        ProviderApplicationStatus.MoreInformationRequired,
        ProviderApplicationStatus.Rejected
    ];

    public async Task<ProviderApplicationDto?> GetMyApplicationAsync(string userId)
    {
        if (!Guid.TryParse(userId, out var userGuid))
            throw new UnauthorizedAccessException("Authentication is required.");

        var profile = await context.ServiceProviderProfiles
            .AsNoTracking()
            .Include(p => p.ServiceCategory)
            .Include(p => p.ServiceZone)
            .FirstOrDefaultAsync(p => p.UserId == userGuid);

        return profile == null ? null : ToDto(profile);
    }

    public async Task<ProviderApplicationDto> UpsertMyApplicationAsync(string userId, UpsertProviderApplicationDto dto)
    {
        var user = await GetActiveUserAsync(userId);
        ValidateApplication(dto);
        var category = await GetActiveCategoryAsync(dto.ServiceCategoryId);
        var zone = await GetActiveZoneAsync(dto.ServiceZoneId);

        var profile = await context.ServiceProviderProfiles.FirstOrDefaultAsync(p => p.UserId == user.Id);
        if (profile == null)
        {
            profile = new ServiceProviderProfile
            {
                UserId = user.Id,
                Status = ProviderApplicationStatus.Draft,
                CreatedAt = DateTime.UtcNow
            };
            context.ServiceProviderProfiles.Add(profile);
        }
        else if (!EditableStatuses.Contains(profile.Status))
        {
            throw new BadRequestException("Provider application cannot be edited in its current status.");
        }

        Apply(dto, profile, category, zone);
        profile.ServiceCategory = category;
        profile.ServiceZone = zone;
        profile.UpdatedAt = DateTime.UtcNow;

        await context.SaveChangesAsync();
        return ToDto(profile);
    }

    public async Task<ProviderApplicationDto> SubmitMyApplicationAsync(string userId)
    {
        var user = await GetActiveUserAsync(userId);
        var profile = await context.ServiceProviderProfiles.FirstOrDefaultAsync(p => p.UserId == user.Id)
            ?? throw new BadRequestException("Create a provider application before submitting it.");

        if (!EditableStatuses.Contains(profile.Status))
            throw new BadRequestException("Provider application cannot be submitted in its current status.");

        ValidateProfileForSubmission(profile);

        profile.Status = ProviderApplicationStatus.Submitted;
        profile.SubmittedAt = DateTime.UtcNow;
        profile.UpdatedAt = DateTime.UtcNow;
        profile.ReviewNotes = null;
        profile.RejectionReason = null;

        user.IsKycSubmitted = true;
        user.IsKycApproved = false;
        user.UpdatedDate = DateTime.UtcNow;

        await context.SaveChangesAsync();
        return ToDto(profile);
    }

    public async Task<IReadOnlyList<ProviderApplicationDto>> GetForAdminAsync(ProviderApplicationStatus? status)
    {
        var query = context.ServiceProviderProfiles.AsNoTracking();
        if (status.HasValue)
            query = query.Where(p => p.Status == status.Value);

        return await query
            .Include(p => p.ServiceCategory)
            .Include(p => p.ServiceZone)
            .OrderByDescending(p => p.SubmittedAt ?? p.CreatedAt)
            .Select(p => ToDto(p))
            .ToListAsync();
    }

    public async Task<ProviderApplicationDto> ReviewAsync(Guid applicationId, string adminUserId, ReviewProviderApplicationDto dto)
    {
        if (string.IsNullOrWhiteSpace(adminUserId))
            throw new UnauthorizedAccessException("Admin identity is required.");

        var profile = await context.ServiceProviderProfiles
            .Include(p => p.User)
            .ThenInclude(u => u.UserRoles)
            .ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(p => p.Id == applicationId)
            ?? throw new NotFoundException("Provider application not found.");

        ValidateReview(dto);

        profile.Status = dto.Status;
        profile.ReviewedAt = DateTime.UtcNow;
        profile.ReviewedByUserId = adminUserId;
        profile.ReviewNotes = NormalizeOptional(dto.ReviewNotes);
        profile.RejectionReason = NormalizeOptional(dto.RejectionReason);
        profile.UpdatedAt = DateTime.UtcNow;

        if (dto.Status == ProviderApplicationStatus.Approved)
        {
            profile.User.IsKycSubmitted = true;
            profile.User.IsKycApproved = true;
            profile.User.KycApprovedByUserId = adminUserId;
            profile.User.KycApprovedOn = DateTime.UtcNow;
            profile.User.IsActive = true;
            profile.User.UpdatedDate = DateTime.UtcNow;
            await EnsureProviderRoleAsync(profile.User);
        }
        else if (dto.Status is ProviderApplicationStatus.Rejected or ProviderApplicationStatus.Suspended or ProviderApplicationStatus.Revoked)
        {
            profile.User.IsKycApproved = false;
            profile.User.UpdatedDate = DateTime.UtcNow;
        }
        else if (dto.Status == ProviderApplicationStatus.MoreInformationRequired)
        {
            profile.User.IsKycApproved = false;
            profile.User.UpdatedDate = DateTime.UtcNow;
        }

        await context.SaveChangesAsync();
        return ToDto(profile);
    }

    public async Task<string?> GetApprovedServiceAreaLabelAsync(string providerUserId)
    {
        if (!Guid.TryParse(providerUserId, out var providerGuid))
            return null;

        var profile = await context.ServiceProviderProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.UserId == providerGuid && p.Status == ProviderApplicationStatus.Approved);

        if (profile == null)
            return null;

        return FormatServiceArea(profile);
    }

    private async Task<User> GetActiveUserAsync(string userId)
    {
        if (!Guid.TryParse(userId, out var userGuid))
            throw new UnauthorizedAccessException("Authentication is required.");

        var user = await context.Users.FirstOrDefaultAsync(u => u.Id == userGuid)
            ?? throw new NotFoundException("User not found.");

        if (!user.IsActive)
            throw new ForbiddenException("Account is not active.");

        return user;
    }

    private async Task EnsureProviderRoleAsync(User user)
    {
        var role = await context.Roles.FirstOrDefaultAsync(r => r.Name == RoleConstants.ServiceProvider);
        if (role == null)
        {
            role = new Role
            {
                Name = RoleConstants.ServiceProvider,
                Description = "Service provider account role",
                CreatedDate = DateTime.UtcNow
            };
            context.Roles.Add(role);
            await context.SaveChangesAsync();
        }

        var hasProviderRole = user.UserRoles.Any(ur =>
            string.Equals(ur.Role.Name, RoleConstants.ServiceProvider, StringComparison.OrdinalIgnoreCase));

        if (!hasProviderRole)
            user.UserRoles.Add(new ServiceMarketplace.Domain.Entities.UserRole { UserId = user.Id, RoleId = role.Id, Role = role });

        if (user.UserType == UserType.Customer)
            user.UserType = UserType.Provider;
    }

    private async Task<ServiceCategory?> GetActiveCategoryAsync(Guid? categoryId)
    {
        if (!categoryId.HasValue)
            return null;

        return await context.ServiceCategories.FirstOrDefaultAsync(c => c.Id == categoryId.Value && c.IsActive)
            ?? throw new BadRequestException("Selected service category is not available.");
    }

    private async Task<ServiceZone?> GetActiveZoneAsync(Guid? zoneId)
    {
        if (!zoneId.HasValue)
            return null;

        return await context.ServiceZones.FirstOrDefaultAsync(z => z.Id == zoneId.Value && z.IsActive)
            ?? throw new BadRequestException("Selected service zone is not available.");
    }

    private static void Apply(
        UpsertProviderApplicationDto dto,
        ServiceProviderProfile profile,
        ServiceCategory? category,
        ServiceZone? zone)
    {
        profile.DisplayName = NormalizeRequired(dto.DisplayName);
        profile.BusinessName = NormalizeOptional(dto.BusinessName);
        profile.Bio = NormalizeOptional(dto.Bio);
        profile.Skills = NormalizeRequired(dto.Skills);
        profile.ServiceCategoryId = category?.Id;
        profile.PrimaryCategory = category?.Name ?? NormalizeRequired(dto.PrimaryCategory);
        profile.ServiceZoneId = zone?.Id;
        profile.ServiceAreaCity = zone?.City ?? NormalizeRequired(dto.ServiceAreaCity);
        profile.ServiceAreaState = zone?.State ?? NormalizeRequired(dto.ServiceAreaState);
        profile.ServiceAreaZone = zone?.ZoneName ?? NormalizeOptional(dto.ServiceAreaZone);
        profile.HourlyRate = dto.HourlyRate;
        profile.IsAvailable = dto.IsAvailable;
        profile.IdentityVerificationSubmitted = dto.IdentityVerificationSubmitted;
        profile.AddressVerificationSubmitted = dto.AddressVerificationSubmitted;
        profile.BackgroundCheckConsent = dto.BackgroundCheckConsent;
    }

    private static void ValidateApplication(UpsertProviderApplicationDto dto)
    {
        _ = NormalizeRequired(dto.DisplayName);
        _ = NormalizeRequired(dto.Skills);
        if (!dto.ServiceCategoryId.HasValue)
            _ = NormalizeRequired(dto.PrimaryCategory);

        if (!dto.ServiceZoneId.HasValue)
        {
            _ = NormalizeRequired(dto.ServiceAreaCity);
            _ = NormalizeRequired(dto.ServiceAreaState);
        }

        if (dto.HourlyRate < 0)
            throw new BadRequestException("Hourly rate cannot be negative.");
    }

    private static void ValidateProfileForSubmission(ServiceProviderProfile profile)
    {
        if (!profile.IdentityVerificationSubmitted ||
            !profile.AddressVerificationSubmitted ||
            !profile.BackgroundCheckConsent)
        {
            throw new BadRequestException("Identity verification, address verification, and background-check consent are required before submission.");
        }

        if (profile.HourlyRate <= 0)
            throw new BadRequestException("Hourly rate must be greater than zero before submission.");
    }

    private static void ValidateReview(ReviewProviderApplicationDto dto)
    {
        if (dto.Status is ProviderApplicationStatus.Draft or ProviderApplicationStatus.Submitted)
            throw new BadRequestException("Admin review must move the application to a review outcome status.");

        if ((dto.Status is ProviderApplicationStatus.Rejected or ProviderApplicationStatus.MoreInformationRequired) &&
            string.IsNullOrWhiteSpace(dto.RejectionReason) &&
            string.IsNullOrWhiteSpace(dto.ReviewNotes))
        {
            throw new BadRequestException("Review notes or a rejection reason are required for this status.");
        }
    }

    private static ProviderApplicationDto ToDto(ServiceProviderProfile profile)
    {
        return new ProviderApplicationDto
        {
            Id = profile.Id,
            UserId = profile.UserId,
            DisplayName = profile.DisplayName,
            BusinessName = profile.BusinessName,
            Bio = profile.Bio,
            Skills = profile.Skills,
            PrimaryCategory = profile.PrimaryCategory,
            ServiceCategoryId = profile.ServiceCategoryId,
            ServiceCategoryName = profile.ServiceCategory?.Name,
            ServiceAreaCity = profile.ServiceAreaCity,
            ServiceAreaState = profile.ServiceAreaState,
            ServiceAreaZone = profile.ServiceAreaZone,
            ServiceZoneId = profile.ServiceZoneId,
            ServiceZoneName = profile.ServiceZone?.DisplayName,
            HourlyRate = profile.HourlyRate,
            IsAvailable = profile.IsAvailable,
            Status = profile.Status,
            IdentityVerificationSubmitted = profile.IdentityVerificationSubmitted,
            AddressVerificationSubmitted = profile.AddressVerificationSubmitted,
            BackgroundCheckConsent = profile.BackgroundCheckConsent,
            CreatedAt = profile.CreatedAt,
            UpdatedAt = profile.UpdatedAt,
            SubmittedAt = profile.SubmittedAt,
            ReviewedAt = profile.ReviewedAt,
            ReviewedByUserId = profile.ReviewedByUserId,
            ReviewNotes = profile.ReviewNotes,
            RejectionReason = profile.RejectionReason
        };
    }

    private static string FormatServiceArea(ServiceProviderProfile profile)
    {
        var parts = new[] { profile.ServiceAreaZone, profile.ServiceAreaCity, profile.ServiceAreaState }
            .Where(part => !string.IsNullOrWhiteSpace(part));

        return string.Join(", ", parts);
    }

    private static string NormalizeRequired(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new BadRequestException("Required provider application fields cannot be empty.");

        return value.Trim();
    }

    private static string? NormalizeOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
