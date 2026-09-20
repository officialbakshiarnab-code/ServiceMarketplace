using Microsoft.EntityFrameworkCore;
using ServiceMarketplace.Application.Constants;
using ServiceMarketplace.Application.DTOs;
using ServiceMarketplace.Application.Exceptions;
using ServiceMarketplace.Application.Interfaces;
using ServiceMarketplace.Domain.Entities;
using ServiceMarketplace.Domain.Enums;
using ServiceMarketplace.Infrastructure.Data;

namespace ServiceMarketplace.Infrastructure.Services;

public sealed class ServicePackageService(
    AppDbContext context,
    IServiceOrderService orderService,
    IServiceOrderAuditService auditService,
    INotificationService notificationService,
    IConversationService conversationService) : IServicePackageService
{
    public async Task<List<ServicePackageDto>> GetActiveAsync(Guid? categoryId = null, Guid? zoneId = null)
    {
        var query = BaseQuery()
            .Where(p => p.IsActive && p.ServiceCategory.IsActive);

        if (categoryId.HasValue)
            query = query.Where(p => p.ServiceCategoryId == categoryId.Value);

        if (zoneId.HasValue)
            query = query.Where(p => !p.ServiceZoneId.HasValue || p.ServiceZoneId == zoneId.Value);

        var packages = await query
            .OrderBy(p => p.Price)
            .ThenBy(p => p.Title)
            .ToListAsync();

        return await ToDtosAsync(packages);
    }

    public async Task<List<ServicePackageDto>> GetMineAsync(string providerId)
    {
        if (string.IsNullOrWhiteSpace(providerId))
            return [];

        var packages = await BaseQuery()
            .Where(p => p.ProviderId == providerId)
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync();

        return await ToDtosAsync(packages);
    }

    public async Task<ServicePackageDto> CreateAsync(string providerId, UpsertServicePackageDto dto)
    {
        var providerProfile = await EnsureApprovedProviderAsync(providerId);
        await ValidatePackageAsync(providerProfile, dto);

        var package = new ServicePackage
        {
            ProviderId = providerId,
            ServiceCategoryId = dto.ServiceCategoryId,
            ServiceZoneId = dto.ServiceZoneId,
            Title = NormalizeRequired(dto.Title, 150, "Package title"),
            Description = NormalizeRequired(dto.Description, 1000, "Package description"),
            Price = dto.Price,
            EstimatedDurationMinutes = dto.EstimatedDurationMinutes,
            IsActive = dto.IsActive
        };

        context.ServicePackages.Add(package);
        await context.SaveChangesAsync();

        return ToDto(package, providerProfile);
    }

    public async Task<ServicePackageDto> UpdateAsync(Guid packageId, string providerId, UpsertServicePackageDto dto)
    {
        var package = await context.ServicePackages
            .Include(p => p.ServiceCategory)
            .Include(p => p.ServiceZone)
            .FirstOrDefaultAsync(p => p.Id == packageId && p.ProviderId == providerId)
            ?? throw new NotFoundException("Service package not found.");

        var providerProfile = await EnsureApprovedProviderAsync(providerId);
        await ValidatePackageAsync(providerProfile, dto);

        package.ServiceCategoryId = dto.ServiceCategoryId;
        package.ServiceZoneId = dto.ServiceZoneId;
        package.Title = NormalizeRequired(dto.Title, 150, "Package title");
        package.Description = NormalizeRequired(dto.Description, 1000, "Package description");
        package.Price = dto.Price;
        package.EstimatedDurationMinutes = dto.EstimatedDurationMinutes;
        package.IsActive = dto.IsActive;
        package.UpdatedAt = DateTime.UtcNow;

        await context.SaveChangesAsync();

        package.ServiceCategory = await context.ServiceCategories.AsNoTracking().FirstAsync(c => c.Id == package.ServiceCategoryId);
        package.ServiceZone = package.ServiceZoneId.HasValue
            ? await context.ServiceZones.AsNoTracking().FirstOrDefaultAsync(z => z.Id == package.ServiceZoneId)
            : null;

        return ToDto(package, providerProfile);
    }

    public async Task<ServiceOrderDto> BookAsync(Guid packageId, string customerId, BookServicePackageDto dto)
    {
        return await context.ExecuteAtomicAsync(async () =>
        {
            if (string.IsNullOrWhiteSpace(customerId))
                throw new UnauthorizedAccessException("Authentication is required.");

            var package = await BaseQuery()
                .FirstOrDefaultAsync(p => p.Id == packageId && p.IsActive && p.ServiceCategory.IsActive)
                ?? throw new NotFoundException("Service package not found.");

            if (package.ProviderId == customerId)
                throw new ForbiddenException("You cannot book your own service package.");

            var scheduledUtc = NormalizeToUtc(dto.ScheduledStartAt);
            if (scheduledUtc <= DateTime.UtcNow.AddMinutes(-1))
                throw new BadRequestException("Scheduled start time must be in the future.");

            var location = NormalizeRequired(dto.Location, 500, "Location");
            var requirements = NormalizeOptional(dto.Requirements, 2000);

            var request = new ServiceRequest
            {
                CustomerId = customerId,
                Title = package.Title,
                Description = package.Description,
                Category = package.ServiceCategory.Name,
                ServiceCategoryId = package.ServiceCategoryId,
                Location = location,
                ServiceZoneId = package.ServiceZoneId,
                Latitude = dto.Latitude,
                Longitude = dto.Longitude,
                Urgency = ServiceRequestUrgency.Flexible,
                PreferredStartAt = scheduledUtc,
                Requirements = requirements,
                Status = ServiceRequestStatus.Accepted,
                CreatedAt = DateTime.UtcNow
            };

            var order = new ServiceOrder
            {
                ServiceRequest = request,
                AcceptedBidId = null,
                ServicePackageId = package.Id,
                CustomerId = customerId,
                ProviderId = package.ProviderId,
                AgreedAmount = package.Price,
                ScheduledStartAt = scheduledUtc,
                EstimatedDurationMinutes = package.EstimatedDurationMinutes,
                Status = ServiceOrderStatus.PendingStart,
                CreatedAt = DateTime.UtcNow
            };

            context.ServiceRequests.Add(request);
            context.ServiceOrders.Add(order);
            await context.SaveChangesAsync();

            await auditService.RecordAsync(
                order,
                customerId,
                "Customer",
                "ServiceOrderCreatedFromPackage",
                null,
                order.Status.ToString(),
                $"Booked package {package.Id}.");
            await conversationService.EnsureServiceOrderConversationAsync(
                order.Id,
                "Order confirmed from a fixed-price service package. You can now use this secure service-order chat.",
                $"service-order:{order.Id:N}:created-from-package");

            await notificationService.NotifyServiceOrderCreatedAsync(order.Id);

            return await orderService.GetByIdAsync(order.Id, customerId);
        });
    }

    private IQueryable<ServicePackage> BaseQuery()
    {
        return context.ServicePackages
            .AsNoTracking()
            .Include(p => p.ServiceCategory)
            .Include(p => p.ServiceZone);
    }

    private async Task<ServiceProviderProfile> EnsureApprovedProviderAsync(string providerId)
    {
        if (!Guid.TryParse(providerId, out var providerGuid))
            throw new ForbiddenException("Provider approval is required before managing service packages.");

        var provider = await context.Users
            .Include(u => u.UserRoles)
            .ThenInclude(ur => ur.Role)
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == providerGuid);

        var roles = provider?.UserRoles
            .Select(ur => ur.Role.Name)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .ToList() ?? [];

        var hasProviderCapability = MarketplaceCapabilityConstants.FromRoles(roles)
            .Contains(MarketplaceCapabilityConstants.ServiceProvider);

        var profile = await context.ServiceProviderProfiles
            .AsNoTracking()
            .Include(p => p.ServiceCategory)
            .Include(p => p.ServiceZone)
            .FirstOrDefaultAsync(p => p.UserId == providerGuid && p.Status == ProviderApplicationStatus.Approved);

        var legacyApprovedProvider = provider?.IsKycApproved == true && hasProviderCapability;

        if (provider == null || !provider.IsActive || !hasProviderCapability || (profile == null && !legacyApprovedProvider))
            throw new ForbiddenException("Provider approval is required before managing service packages.");

        if (profile is not { IsAvailable: true })
            throw new ForbiddenException("Provider availability must be enabled before managing service packages.");

        return profile;
    }

    private async Task ValidatePackageAsync(ServiceProviderProfile providerProfile, UpsertServicePackageDto dto)
    {
        if (dto.Price <= 0)
            throw new BadRequestException("Package price must be greater than zero.");

        if (dto.EstimatedDurationMinutes is <= 0 or > 10080)
            throw new BadRequestException("Estimated duration must be between 1 minute and 7 days.");

        if (providerProfile.ServiceCategoryId.HasValue && dto.ServiceCategoryId != providerProfile.ServiceCategoryId)
            throw new BadRequestException("Package category must match the approved provider category.");

        if (providerProfile.ServiceZoneId.HasValue && dto.ServiceZoneId.HasValue && dto.ServiceZoneId != providerProfile.ServiceZoneId)
            throw new BadRequestException("Package zone must match the approved provider zone.");

        var categoryExists = await context.ServiceCategories
            .AsNoTracking()
            .AnyAsync(c => c.Id == dto.ServiceCategoryId && c.IsActive);

        if (!categoryExists)
            throw new BadRequestException("Selected service category is not available.");

        if (dto.ServiceZoneId.HasValue)
        {
            var zoneExists = await context.ServiceZones
                .AsNoTracking()
                .AnyAsync(z => z.Id == dto.ServiceZoneId.Value && z.IsActive);

            if (!zoneExists)
                throw new BadRequestException("Selected service zone is not available.");
        }

        _ = NormalizeRequired(dto.Title, 150, "Package title");
        _ = NormalizeRequired(dto.Description, 1000, "Package description");
    }

    private async Task<List<ServicePackageDto>> ToDtosAsync(List<ServicePackage> packages)
    {
        var providerIds = packages
            .Select(p => Guid.TryParse(p.ProviderId, out var id) ? id : (Guid?)null)
            .Where(id => id.HasValue)
            .Select(id => id!.Value)
            .ToList();

        var profiles = await context.ServiceProviderProfiles
            .AsNoTracking()
            .Where(p => providerIds.Contains(p.UserId))
            .ToDictionaryAsync(p => p.UserId);

        return packages.Select(package =>
        {
            ServiceProviderProfile? profile = null;
            if (Guid.TryParse(package.ProviderId, out var providerGuid))
                profiles.TryGetValue(providerGuid, out profile);

            return ToDto(package, profile);
        }).ToList();
    }

    private static ServicePackageDto ToDto(ServicePackage package, ServiceProviderProfile? providerProfile)
    {
        return new ServicePackageDto
        {
            Id = package.Id,
            ProviderId = package.ProviderId,
            ProviderDisplayName = providerProfile?.DisplayName ?? "Service Provider",
            ProviderBusinessName = providerProfile?.BusinessName,
            ProviderAverageRating = providerProfile?.AverageRating ?? 0m,
            ProviderReviewCount = providerProfile?.ReviewCount ?? 0,
            ServiceCategoryId = package.ServiceCategoryId,
            ServiceCategoryName = package.ServiceCategory?.Name ?? providerProfile?.PrimaryCategory ?? string.Empty,
            ServiceZoneId = package.ServiceZoneId,
            ServiceZoneName = package.ServiceZone?.DisplayName ?? providerProfile?.ServiceAreaZone,
            Title = package.Title,
            Description = package.Description,
            Price = package.Price,
            EstimatedDurationMinutes = package.EstimatedDurationMinutes,
            IsActive = package.IsActive,
            CreatedAt = package.CreatedAt,
            UpdatedAt = package.UpdatedAt
        };
    }

    private static DateTime NormalizeToUtc(DateTime value)
    {
        return value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
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
