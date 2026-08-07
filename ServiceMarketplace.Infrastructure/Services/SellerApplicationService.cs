using Microsoft.EntityFrameworkCore;
using ServiceMarketplace.Application.DTOs;
using ServiceMarketplace.Application.Exceptions;
using ServiceMarketplace.Application.Interfaces;
using ServiceMarketplace.Domain.Entities;
using ServiceMarketplace.Domain.Enums;
using ServiceMarketplace.Infrastructure.Data;

namespace ServiceMarketplace.Infrastructure.Services;

public sealed class SellerApplicationService(AppDbContext context) : ISellerApplicationService
{
    private static readonly SellerApplicationStatus[] EditableStatuses =
    [
        SellerApplicationStatus.Draft,
        SellerApplicationStatus.MoreInformationRequired,
        SellerApplicationStatus.Rejected
    ];

    public async Task<SellerApplicationDto?> GetMyApplicationAsync(string userId)
    {
        if (!Guid.TryParse(userId, out var userGuid))
            throw new UnauthorizedAccessException("Authentication is required.");

        var profile = await context.SellerProfiles
            .AsNoTracking()
            .Include(s => s.ServiceZone)
            .FirstOrDefaultAsync(s => s.UserId == userGuid);

        return profile == null ? null : ToDto(profile);
    }

    public async Task<SellerApplicationDto> UpsertMyApplicationAsync(string userId, UpsertSellerApplicationDto dto)
    {
        var user = await GetActiveUserAsync(userId);
        ValidateApplication(dto);
        var zone = await GetActiveZoneAsync(dto.ServiceZoneId);

        var profile = await context.SellerProfiles.FirstOrDefaultAsync(s => s.UserId == user.Id);
        if (profile == null)
        {
            profile = new SellerProfile
            {
                UserId = user.Id,
                Status = SellerApplicationStatus.Draft,
                CreatedAt = DateTime.UtcNow
            };
            context.SellerProfiles.Add(profile);
        }
        else if (!EditableStatuses.Contains(profile.Status))
        {
            throw new BadRequestException("Seller application cannot be edited in its current status.");
        }

        Apply(dto, profile, zone);
        profile.ServiceZone = zone;
        profile.UpdatedAt = DateTime.UtcNow;

        await context.SaveChangesAsync();
        return ToDto(profile);
    }

    public async Task<SellerApplicationDto> SubmitMyApplicationAsync(string userId)
    {
        var user = await GetActiveUserAsync(userId);
        var profile = await context.SellerProfiles.FirstOrDefaultAsync(s => s.UserId == user.Id)
            ?? throw new BadRequestException("Create a seller application before submitting it.");

        if (!EditableStatuses.Contains(profile.Status))
            throw new BadRequestException("Seller application cannot be submitted in its current status.");

        ValidateProfileForSubmission(profile);

        profile.Status = SellerApplicationStatus.Submitted;
        profile.SubmittedAt = DateTime.UtcNow;
        profile.UpdatedAt = DateTime.UtcNow;
        profile.ReviewNotes = null;
        profile.RejectionReason = null;

        await context.SaveChangesAsync();
        return ToDto(profile);
    }

    public async Task<IReadOnlyList<SellerApplicationDto>> GetForAdminAsync(SellerApplicationStatus? status)
    {
        var query = context.SellerProfiles.AsNoTracking();
        if (status.HasValue)
            query = query.Where(s => s.Status == status.Value);

        return await query
            .Include(s => s.ServiceZone)
            .OrderByDescending(s => s.SubmittedAt ?? s.CreatedAt)
            .Select(s => ToDto(s))
            .ToListAsync();
    }

    public async Task<SellerApplicationDto> ReviewAsync(Guid applicationId, string adminUserId, ReviewSellerApplicationDto dto)
    {
        if (string.IsNullOrWhiteSpace(adminUserId))
            throw new UnauthorizedAccessException("Admin identity is required.");

        var profile = await context.SellerProfiles
            .Include(s => s.ServiceZone)
            .FirstOrDefaultAsync(s => s.Id == applicationId)
            ?? throw new NotFoundException("Seller application not found.");

        ValidateReview(dto);

        profile.Status = dto.Status;
        profile.ReviewedAt = DateTime.UtcNow;
        profile.ReviewedByUserId = adminUserId;
        profile.ReviewNotes = NormalizeOptional(dto.ReviewNotes, 1000);
        profile.RejectionReason = NormalizeOptional(dto.RejectionReason, 1000);
        profile.UpdatedAt = DateTime.UtcNow;

        await context.SaveChangesAsync();
        return ToDto(profile);
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

    private async Task<ServiceZone?> GetActiveZoneAsync(Guid? zoneId)
    {
        if (!zoneId.HasValue)
            return null;

        return await context.ServiceZones.FirstOrDefaultAsync(z => z.Id == zoneId.Value && z.IsActive)
            ?? throw new BadRequestException("Selected seller zone is not available.");
    }

    private static void Apply(UpsertSellerApplicationDto dto, SellerProfile profile, ServiceZone? zone)
    {
        profile.StoreName = NormalizeRequired(dto.StoreName, 150, "Store name");
        profile.BusinessName = NormalizeOptional(dto.BusinessName, 150);
        profile.Description = NormalizeOptional(dto.Description, 1000);
        profile.Gstin = NormalizeOptional(dto.Gstin, 30);
        profile.PickupAddress = NormalizeRequired(dto.PickupAddress, 500, "Pickup address");
        profile.City = zone?.City ?? NormalizeRequired(dto.City, 100, "City");
        profile.State = zone?.State ?? NormalizeRequired(dto.State, 100, "State");
        profile.ServiceZoneId = zone?.Id;
        profile.IdentityVerificationSubmitted = dto.IdentityVerificationSubmitted;
        profile.AddressVerificationSubmitted = dto.AddressVerificationSubmitted;
        profile.BusinessVerificationSubmitted = dto.BusinessVerificationSubmitted;
    }

    private static void ValidateApplication(UpsertSellerApplicationDto dto)
    {
        _ = NormalizeRequired(dto.StoreName, 150, "Store name");
        _ = NormalizeRequired(dto.PickupAddress, 500, "Pickup address");

        if (!dto.ServiceZoneId.HasValue)
        {
            _ = NormalizeRequired(dto.City, 100, "City");
            _ = NormalizeRequired(dto.State, 100, "State");
        }
    }

    private static void ValidateProfileForSubmission(SellerProfile profile)
    {
        if (!profile.IdentityVerificationSubmitted || !profile.AddressVerificationSubmitted)
            throw new BadRequestException("Identity and address verification are required before seller submission.");

        _ = NormalizeRequired(profile.StoreName, 150, "Store name");
        _ = NormalizeRequired(profile.PickupAddress, 500, "Pickup address");
        _ = NormalizeRequired(profile.City, 100, "City");
        _ = NormalizeRequired(profile.State, 100, "State");
    }

    private static void ValidateReview(ReviewSellerApplicationDto dto)
    {
        if (dto.Status is SellerApplicationStatus.Draft or SellerApplicationStatus.Submitted)
            throw new BadRequestException("Admin review must move the seller application to a review outcome status.");

        if ((dto.Status is SellerApplicationStatus.Rejected or SellerApplicationStatus.MoreInformationRequired) &&
            string.IsNullOrWhiteSpace(dto.RejectionReason) &&
            string.IsNullOrWhiteSpace(dto.ReviewNotes))
        {
            throw new BadRequestException("Review notes or a rejection reason are required for this status.");
        }
    }

    private static SellerApplicationDto ToDto(SellerProfile profile)
    {
        return new SellerApplicationDto
        {
            Id = profile.Id,
            UserId = profile.UserId,
            StoreName = profile.StoreName,
            BusinessName = profile.BusinessName,
            Description = profile.Description,
            Gstin = profile.Gstin,
            PickupAddress = profile.PickupAddress,
            City = profile.City,
            State = profile.State,
            ServiceZoneId = profile.ServiceZoneId,
            ServiceZoneName = profile.ServiceZone?.DisplayName,
            IdentityVerificationSubmitted = profile.IdentityVerificationSubmitted,
            AddressVerificationSubmitted = profile.AddressVerificationSubmitted,
            BusinessVerificationSubmitted = profile.BusinessVerificationSubmitted,
            Status = profile.Status,
            CreatedAt = profile.CreatedAt,
            UpdatedAt = profile.UpdatedAt,
            SubmittedAt = profile.SubmittedAt,
            ReviewedAt = profile.ReviewedAt,
            ReviewedByUserId = profile.ReviewedByUserId,
            ReviewNotes = profile.ReviewNotes,
            RejectionReason = profile.RejectionReason
        };
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
}
