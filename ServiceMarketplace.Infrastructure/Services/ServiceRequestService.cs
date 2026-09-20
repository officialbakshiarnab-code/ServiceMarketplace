using Microsoft.EntityFrameworkCore;
using ServiceMarketplace.Application.Constants;
using ServiceMarketplace.Application.DTOs;
using ServiceMarketplace.Application.Exceptions;
using ServiceMarketplace.Application.Interfaces;
using ServiceMarketplace.Domain.Entities;
using ServiceMarketplace.Domain.Enums;
using ServiceMarketplace.Infrastructure.Data;

namespace ServiceMarketplace.Infrastructure.Services;

// Purpose: Business logic for service requests (create, search, accept bids).
public class ServiceRequestService : IServiceRequestService
{
    private readonly AppDbContext _context;
    private readonly INotificationService _notificationService;
    private readonly IServiceOrderAuditService _auditService;
    private readonly IConversationService _conversationService;

    public ServiceRequestService(
        AppDbContext context,
        INotificationService notificationService,
        IServiceOrderAuditService auditService,
        IConversationService conversationService)
    {
        _context = context;
        _notificationService = notificationService;
        _auditService = auditService;
        _conversationService = conversationService;
    }

    public async Task<Guid> CreateAsync(CreateServiceRequestDto dto, string userId)
    {
        if (string.IsNullOrWhiteSpace(userId))
            throw new UnauthorizedAccessException("Authentication is required.");

        ServiceCategory? category = null;
        if (dto.ServiceCategoryId.HasValue)
        {
            category = await _context.ServiceCategories
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == dto.ServiceCategoryId.Value && c.IsActive);

            if (category == null)
                throw new BadRequestException("Selected service category is not available.");
        }

        ServiceZone? zone = null;
        if (dto.ServiceZoneId.HasValue)
        {
            zone = await _context.ServiceZones
                .AsNoTracking()
                .FirstOrDefaultAsync(z => z.Id == dto.ServiceZoneId.Value && z.IsActive);

            if (zone == null)
                throw new BadRequestException("Selected service zone is not available.");
        }

        var request = new ServiceRequest
        {
            CustomerId = userId,
            Title = dto.Title,
            Description = dto.Description,
            Category = category?.Name ?? dto.Category,
            ServiceCategoryId = category?.Id,
            Location = dto.Location,
            ServiceZoneId = zone?.Id,
            Latitude = dto.Latitude,
            Longitude = dto.Longitude,
            Urgency = dto.Urgency,
            PreferredStartAt = dto.PreferredStartAt,
            Requirements = dto.Requirements,
            Status = ServiceRequestStatus.Open
        };

        _context.ServiceRequests.Add(request);
        await _context.SaveChangesAsync();

        await _notificationService.NotifyNewRequestNearbyAsync(request.Id);

        return request.Id;
    }

    public async Task<IEnumerable<ProviderServiceRequestDto>> GetOpenAsync(string providerUserId)
    {
        var providerProfile = await EnsureApprovedProviderAsync(providerUserId);

        var query = _context.ServiceRequests
            .AsNoTracking()
            .Where(r => r.Status == ServiceRequestStatus.Open && r.CustomerId != providerUserId);

        query = ApplyProviderCoverageFilter(query, providerProfile);

        var requests = await query
            .OrderByDescending(r => r.CreatedAt)
            .Select(r => new
            {
                r.Id,
                r.Title,
                r.Description,
                r.Category,
                r.ServiceCategoryId,
                ServiceCategoryName = r.ServiceCategory != null ? r.ServiceCategory.Name : null,
                r.Location,
                r.ServiceZoneId,
                ServiceZoneName = r.ServiceZone != null ? r.ServiceZone.DisplayName : null,
                r.Status,
                r.Urgency,
                r.PreferredStartAt,
                BidCount = r.Bids.Count,
                r.CreatedAt,
                r.UpdatedAt
            })
            .ToListAsync();

        return requests.Select(r => new ProviderServiceRequestDto
        {
            Id = r.Id,
            Title = r.Title,
            Description = r.Description,
            Category = r.Category,
            ServiceCategoryId = r.ServiceCategoryId,
            ServiceCategoryName = r.ServiceCategoryName,
            ApproximateLocation = ToProviderLocation(r.ServiceZoneName, r.Location),
            ServiceZoneId = r.ServiceZoneId,
            ServiceZoneName = r.ServiceZoneName,
            Status = r.Status,
            Urgency = r.Urgency,
            PreferredStartAt = r.PreferredStartAt,
            BidCount = r.BidCount,
            CreatedAt = r.CreatedAt,
            UpdatedAt = r.UpdatedAt
        });
    }

    public async Task<List<ProviderServiceRequestDto>> GetNearbyAsync(string providerUserId, double latitude, double longitude, double radiusKm)
    {
        var providerProfile = await EnsureApprovedProviderAsync(providerUserId);

        const double EarthRadiusKm = 6371.0;
        var latRad = latitude * (Math.PI / 180.0);
        var lonRad = longitude * (Math.PI / 180.0);

        var query = _context.ServiceRequests
            .AsNoTracking()
            .Where(r => r.Status == ServiceRequestStatus.Open && r.CustomerId != providerUserId);

        query = ApplyProviderCoverageFilter(query, providerProfile);

        var nearbyRequests = await query
            .Select(r => new
            {
                r.Id,
                r.Title,
                r.Description,
                r.Category,
                r.ServiceCategoryId,
                ServiceCategoryName = r.ServiceCategory != null ? r.ServiceCategory.Name : null,
                r.Location,
                r.ServiceZoneId,
                ServiceZoneName = r.ServiceZone != null ? r.ServiceZone.DisplayName : null,
                r.Status,
                r.Urgency,
                r.PreferredStartAt,
                BidCount = r.Bids.Count,
                r.CreatedAt,
                r.UpdatedAt,
                DistanceKm = EarthRadiusKm * 2.0 * Math.Asin(
                    Math.Sqrt(
                        Math.Pow(Math.Sin((((r.Latitude * (Math.PI / 180.0)) - latRad) / 2.0)), 2.0) +
                        Math.Cos(latRad) * Math.Cos(r.Latitude * (Math.PI / 180.0)) *
                        Math.Pow(Math.Sin((((r.Longitude * (Math.PI / 180.0)) - lonRad) / 2.0)), 2.0)
                    )
                )
            })
            .Where(x => x.DistanceKm <= radiusKm)
            .OrderBy(x => x.DistanceKm)
            .ToListAsync();

        return nearbyRequests.Select(x => new ProviderServiceRequestDto
            {
                Id = x.Id,
                Title = x.Title,
                Description = x.Description,
                Category = x.Category,
                ServiceCategoryId = x.ServiceCategoryId,
                ServiceCategoryName = x.ServiceCategoryName,
                ApproximateLocation = ToProviderLocation(x.ServiceZoneName, x.Location),
                ServiceZoneId = x.ServiceZoneId,
                ServiceZoneName = x.ServiceZoneName,
                Status = x.Status,
                Urgency = x.Urgency,
                PreferredStartAt = x.PreferredStartAt,
                DistanceKm = x.DistanceKm,
                BidCount = x.BidCount,
                CreatedAt = x.CreatedAt,
                UpdatedAt = x.UpdatedAt
            })
            .ToList();
    }

    public async Task<List<ServiceRequestDto>> GetMyRequestsAsync(string userId)
    {
        if (string.IsNullOrWhiteSpace(userId))
            return new List<ServiceRequestDto>();

        return await _context.ServiceRequests
            .Where(r => r.CustomerId == userId)
            .OrderByDescending(r => r.CreatedAt)
            .Select(r => new ServiceRequestDto
            {
                Id = r.Id,
                CustomerId = r.CustomerId,
                Title = r.Title,
                Description = r.Description,
                Category = r.Category,
                ServiceCategoryId = r.ServiceCategoryId,
                ServiceCategoryName = r.ServiceCategory != null ? r.ServiceCategory.Name : null,
                Location = r.Location,
                ServiceZoneId = r.ServiceZoneId,
                ServiceZoneName = r.ServiceZone != null ? r.ServiceZone.DisplayName : null,
                Latitude = r.Latitude,
                Longitude = r.Longitude,
                Status = r.Status,
                Urgency = r.Urgency,
                PreferredStartAt = r.PreferredStartAt,
                Requirements = r.Requirements,
                BidCount = r.Bids.Count,
                CreatedAt = r.CreatedAt,
                UpdatedAt = r.UpdatedAt
            })
            .ToListAsync();
    }

    public async Task<List<ProviderServiceRequestDto>> GetAvailableForProviderAsync(string providerUserId)
    {
        if (string.IsNullOrWhiteSpace(providerUserId))
            return new List<ProviderServiceRequestDto>();

        var providerProfile = await EnsureApprovedProviderAsync(providerUserId);

        var query = _context.ServiceRequests
            .AsNoTracking()
            .Where(r => r.Status == ServiceRequestStatus.Open && r.CustomerId != providerUserId);

        query = ApplyProviderCoverageFilter(query, providerProfile);

        var requests = await query
            .OrderByDescending(r => r.CreatedAt)
            .Select(r => new
            {
                r.Id,
                r.Title,
                r.Description,
                r.Category,
                r.ServiceCategoryId,
                ServiceCategoryName = r.ServiceCategory != null ? r.ServiceCategory.Name : null,
                r.Location,
                r.ServiceZoneId,
                ServiceZoneName = r.ServiceZone != null ? r.ServiceZone.DisplayName : null,
                r.Status,
                r.Urgency,
                r.PreferredStartAt,
                BidCount = r.Bids.Count,
                r.CreatedAt,
                r.UpdatedAt
            })
            .ToListAsync();

        return requests.Select(r => new ProviderServiceRequestDto
        {
            Id = r.Id,
            Title = r.Title,
            Description = r.Description,
            Category = r.Category,
            ServiceCategoryId = r.ServiceCategoryId,
            ServiceCategoryName = r.ServiceCategoryName,
            ApproximateLocation = ToProviderLocation(r.ServiceZoneName, r.Location),
            ServiceZoneId = r.ServiceZoneId,
            ServiceZoneName = r.ServiceZoneName,
            Status = r.Status,
            Urgency = r.Urgency,
            PreferredStartAt = r.PreferredStartAt,
            BidCount = r.BidCount,
            CreatedAt = r.CreatedAt,
            UpdatedAt = r.UpdatedAt
        }).ToList();
    }

    public async Task<ServiceRequestDto> GetByIdForUserAsync(Guid requestId, string userId)
    {
        if (string.IsNullOrWhiteSpace(userId))
            throw new ForbiddenException("User ID is required");

        var request = await _context.ServiceRequests
            .Where(r => r.Id == requestId && r.CustomerId == userId)
            .Select(r => new ServiceRequestDto
            {
                Id = r.Id,
                CustomerId = r.CustomerId,
                Title = r.Title,
                Description = r.Description,
                Category = r.Category,
                ServiceCategoryId = r.ServiceCategoryId,
                ServiceCategoryName = r.ServiceCategory != null ? r.ServiceCategory.Name : null,
                Location = r.Location,
                ServiceZoneId = r.ServiceZoneId,
                ServiceZoneName = r.ServiceZone != null ? r.ServiceZone.DisplayName : null,
                Latitude = r.Latitude,
                Longitude = r.Longitude,
                Status = r.Status,
                Urgency = r.Urgency,
                PreferredStartAt = r.PreferredStartAt,
                Requirements = r.Requirements,
                BidCount = r.Bids.Count,
                CreatedAt = r.CreatedAt,
                UpdatedAt = r.UpdatedAt
            })
            .FirstOrDefaultAsync();

        if (request == null)
            throw new NotFoundException("Service request not found");

        return request;
    }

    public async Task<ProviderServiceRequestDto> GetByIdForProviderAsync(Guid requestId, string providerId)
    {
        if (string.IsNullOrWhiteSpace(providerId))
            throw new ForbiddenException("Provider ID is required");

        var providerProfile = await EnsureApprovedProviderAsync(providerId);

        var request = await _context.ServiceRequests
            .AsNoTracking()
            .Where(r => r.Id == requestId && r.CustomerId != providerId)
            .Select(r => new
            {
                r.Id,
                r.Title,
                r.Description,
                r.Category,
                r.ServiceCategoryId,
                ServiceCategoryName = r.ServiceCategory != null ? r.ServiceCategory.Name : null,
                r.Location,
                r.ServiceZoneId,
                ServiceZoneName = r.ServiceZone != null ? r.ServiceZone.DisplayName : null,
                r.Status,
                r.Urgency,
                r.PreferredStartAt,
                IsAcceptedForProvider = r.Bids.Any(b => b.ServiceProviderId == providerId && b.Status == BidStatus.Accepted),
                r.Latitude,
                r.Longitude,
                BidCount = r.Bids.Count,
                r.CreatedAt,
                r.UpdatedAt
            })
            .FirstOrDefaultAsync();

        if (request == null ||
            (request.Status != ServiceRequestStatus.Open && !request.IsAcceptedForProvider) ||
            (request.Status == ServiceRequestStatus.Open && !CanServeRequest(providerProfile, request.ServiceCategoryId, request.ServiceCategoryName, request.Category, request.ServiceZoneId, request.ServiceZoneName, request.Location)))
        {
            throw new NotFoundException("Service request not found or not available for bidding");
        }

        return new ProviderServiceRequestDto
        {
            Id = request.Id,
            Title = request.Title,
            Description = request.Description,
            Category = request.Category,
            ServiceCategoryId = request.ServiceCategoryId,
            ServiceCategoryName = request.ServiceCategoryName,
            ApproximateLocation = ToProviderLocation(request.ServiceZoneName, request.Location),
            ExactLocation = request.IsAcceptedForProvider ? request.Location : null,
            ServiceZoneId = request.ServiceZoneId,
            ServiceZoneName = request.ServiceZoneName,
            Latitude = request.IsAcceptedForProvider ? request.Latitude : null,
            Longitude = request.IsAcceptedForProvider ? request.Longitude : null,
            IsExactLocationVisible = request.IsAcceptedForProvider,
            Status = request.Status,
            Urgency = request.Urgency,
            PreferredStartAt = request.PreferredStartAt,
            BidCount = request.BidCount,
            CreatedAt = request.CreatedAt,
            UpdatedAt = request.UpdatedAt
        };
    }

    public async Task AcceptBidAsync(Guid requestId, Guid bidId, string userId)
    {
        await _context.ExecuteAtomicAsync(async () =>
        {
            if (string.IsNullOrWhiteSpace(userId))
                throw new UnauthorizedAccessException("Authentication is required.");

            var request = await _context.ServiceRequests
                .Include(r => r.Bids)
                .FirstOrDefaultAsync(r => r.Id == requestId);

            if (request == null)
                throw new NotFoundException("Service request not found");

            if (request.CustomerId != userId)
                throw new ForbiddenException("Not authorized to accept bids for this request");

            if (request.Status != ServiceRequestStatus.Open)
                throw new BadRequestException("Request is not open");

            var selectedBid = request.Bids.FirstOrDefault(b => b.Id == bidId);
            if (selectedBid == null)
                throw new NotFoundException("Bid not found");

            var existingOrder = await _context.ServiceOrders.AnyAsync(o => o.ServiceRequestId == requestId);
            if (existingOrder)
                throw new BadRequestException("A service order already exists for this request");

            // Accept selected bid
            selectedBid.Status = BidStatus.Accepted;

            // Reject all others
            foreach (var bid in request.Bids.Where(b => b.Id != bidId))
                bid.Status = BidStatus.Rejected;

            request.Status = ServiceRequestStatus.Accepted;

            var order = new ServiceOrder
            {
                ServiceRequestId = request.Id,
                AcceptedBidId = selectedBid.Id,
                CustomerId = request.CustomerId,
                ProviderId = selectedBid.ServiceProviderId,
                AgreedAmount = selectedBid.Amount,
                ScheduledStartAt = selectedBid.ProposedDateTime,
                EstimatedDurationMinutes = selectedBid.EstimatedDurationMinutes,
                Status = ServiceOrderStatus.PendingStart,
                CreatedAt = DateTime.UtcNow
            };
            _context.ServiceOrders.Add(order);

            await _context.SaveChangesAsync();

            await _auditService.RecordAsync(
                order,
                userId,
                "Customer",
                "ServiceOrderCreated",
                null,
                order.Status.ToString(),
                $"Accepted bid {selectedBid.Id}.");
            await _conversationService.EnsureServiceOrderConversationAsync(
                order.Id,
                "Order confirmed. You can now use this secure service-order chat.",
                $"service-order:{order.Id:N}:created");
            await _notificationService.NotifyBidAcceptedAsync(selectedBid.Id);
            await _notificationService.NotifyServiceOrderCreatedAsync(order.Id);
        });
    }

    public async Task<UserDashboardStatsDto> GetDashboardStatsAsync(string userId)
    {
        if (string.IsNullOrWhiteSpace(userId))
            throw new UnauthorizedAccessException("User ID is required");

        // Efficient single query to get all stats at once (no N+1 problem)
        // Groups requests by status and counts them in a single database roundtrip
        var requestStats = await _context.ServiceRequests
            .Where(r => r.CustomerId == userId)
            .GroupBy(r => 1) // Group all into one group to aggregate
            .Select(g => new
            {
                OpenCount = g.Count(r => r.Status == ServiceRequestStatus.Open),
                CompletedCount = g.Count(r => r.Status == ServiceRequestStatus.Closed),
                TotalCount = g.Count(),
                // Count active bids on open requests (single query)
                ActiveBidsCount = g
                    .Where(r => r.Status == ServiceRequestStatus.Open)
                    .Sum(r => r.Bids.Count)
            })
            .FirstOrDefaultAsync();

        // If user has no requests yet, return zeros
        if (requestStats == null)
        {
            return new UserDashboardStatsDto
            {
                OpenRequestsCount = 0,
                ActiveBidsCount = 0,
                CompletedRequestsCount = 0,
                TotalRequestsCount = 0
            };
        }

        return new UserDashboardStatsDto
        {
            OpenRequestsCount = requestStats.OpenCount,
            ActiveBidsCount = requestStats.ActiveBidsCount,
            CompletedRequestsCount = requestStats.CompletedCount,
            TotalRequestsCount = requestStats.TotalCount
        };
    }

    private async Task<ServiceProviderProfile?> EnsureApprovedProviderAsync(string providerUserId)
    {
        if (string.IsNullOrWhiteSpace(providerUserId) || !Guid.TryParse(providerUserId, out var providerGuid))
            throw new ForbiddenException("Provider approval is required before accessing provider request workflows");

        var provider = await _context.Users
            .Include(u => u.UserRoles)
            .ThenInclude(ur => ur.Role)
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == providerGuid);

        var roles = provider?.UserRoles
            .Select(ur => ur.Role.Name)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .ToList() ?? new List<string>();

        var hasProviderCapability = MarketplaceCapabilityConstants.FromRoles(roles)
            .Contains(MarketplaceCapabilityConstants.ServiceProvider);
        var approvedProfileExists = await _context.ServiceProviderProfiles
            .AsNoTracking()
            .AnyAsync(p => p.UserId == providerGuid && p.Status == ProviderApplicationStatus.Approved);
        var legacyApprovedProvider = provider?.IsKycApproved == true && hasProviderCapability;

        if (provider == null ||
            !hasProviderCapability ||
            !provider.IsActive ||
            (!approvedProfileExists && !legacyApprovedProvider))
        {
            throw new ForbiddenException("Provider approval is required before accessing provider request workflows");
        }

        var approvedProfile = await _context.ServiceProviderProfiles
            .AsNoTracking()
            .Include(p => p.ServiceCategory)
            .Include(p => p.ServiceZone)
            .FirstOrDefaultAsync(p => p.UserId == providerGuid && p.Status == ProviderApplicationStatus.Approved);

        if (approvedProfile is { IsAvailable: false })
            throw new ForbiddenException("Provider availability must be enabled before accessing provider request workflows");

        return approvedProfile;
    }

    private static IQueryable<ServiceRequest> ApplyProviderCoverageFilter(
        IQueryable<ServiceRequest> query,
        ServiceProviderProfile? profile)
    {
        if (profile == null)
            return query;

        if (profile.ServiceCategoryId.HasValue)
        {
            var categoryId = profile.ServiceCategoryId.Value;
            var categoryName = profile.PrimaryCategory.ToUpper();
            query = query.Where(r =>
                (r.ServiceCategoryId.HasValue && r.ServiceCategoryId == categoryId) ||
                (!r.ServiceCategoryId.HasValue && r.Category.ToUpper() == categoryName));
        }
        else if (!string.IsNullOrWhiteSpace(profile.PrimaryCategory))
        {
            var categoryName = profile.PrimaryCategory.ToUpper();
            query = query.Where(r =>
                r.Category.ToUpper() == categoryName ||
                (r.ServiceCategory != null && r.ServiceCategory.Name.ToUpper() == categoryName));
        }

        if (profile.ServiceZoneId.HasValue)
        {
            var zoneId = profile.ServiceZoneId.Value;
            var zoneName = profile.ServiceAreaZone == null ? string.Empty : profile.ServiceAreaZone.ToUpper();
            var city = profile.ServiceAreaCity.ToUpper();
            var state = profile.ServiceAreaState.ToUpper();
            query = query.Where(r =>
                (r.ServiceZoneId.HasValue && r.ServiceZoneId == zoneId) ||
                (!r.ServiceZoneId.HasValue && r.Location.ToUpper().Contains(city) && r.Location.ToUpper().Contains(state)) ||
                (!r.ServiceZoneId.HasValue && zoneName != string.Empty && r.Location.ToUpper().Contains(zoneName)));
        }
        else
        {
            var zoneName = profile.ServiceAreaZone == null ? string.Empty : profile.ServiceAreaZone.ToUpper();
            var city = profile.ServiceAreaCity.ToUpper();
            var state = profile.ServiceAreaState.ToUpper();
            query = query.Where(r =>
                r.Location.ToUpper().Contains(city) ||
                r.Location.ToUpper().Contains(state) ||
                (zoneName != string.Empty && r.Location.ToUpper().Contains(zoneName)) ||
                (r.ServiceZone != null &&
                    (r.ServiceZone.City.ToUpper() == city ||
                     r.ServiceZone.State.ToUpper() == state ||
                     (zoneName != string.Empty && r.ServiceZone.DisplayName.ToUpper().Contains(zoneName)))));
        }

        return query;
    }

    private static bool CanServeRequest(
        ServiceProviderProfile? profile,
        Guid? requestCategoryId,
        string? requestCategoryName,
        string requestCategory,
        Guid? requestZoneId,
        string? requestZoneName,
        string requestLocation)
    {
        if (profile == null)
            return true;

        if (profile.ServiceCategoryId.HasValue && requestCategoryId.HasValue && profile.ServiceCategoryId != requestCategoryId)
            return false;

        if (!profile.ServiceCategoryId.HasValue || !requestCategoryId.HasValue)
        {
            var providerCategory = profile.PrimaryCategory;
            var categoryMatches =
                string.Equals(providerCategory, requestCategory, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(providerCategory, requestCategoryName, StringComparison.OrdinalIgnoreCase);

            if (!categoryMatches)
                return false;
        }

        if (profile.ServiceZoneId.HasValue && requestZoneId.HasValue && profile.ServiceZoneId != requestZoneId)
            return false;

        if (!profile.ServiceZoneId.HasValue || !requestZoneId.HasValue)
        {
            var providerAreaParts = new[] { profile.ServiceAreaZone, profile.ServiceAreaCity, profile.ServiceAreaState }
                .Where(part => !string.IsNullOrWhiteSpace(part))
                .ToList();

            var areaMatches = providerAreaParts.Any(part =>
                requestLocation.Contains(part!, StringComparison.OrdinalIgnoreCase) ||
                (requestZoneName?.Contains(part!, StringComparison.OrdinalIgnoreCase) ?? false));

            if (!areaMatches)
                return false;
        }

        return true;
    }

    private static string ToProviderLocation(string? serviceZoneName, string location)
    {
        return string.IsNullOrWhiteSpace(serviceZoneName)
            ? ToApproximateLocation(location)
            : serviceZoneName;
    }

    private static string ToApproximateLocation(string location)
    {
        if (string.IsNullOrWhiteSpace(location))
            return "Nearby service area";

        var parts = location
            .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .ToList();

        if (parts.Count >= 2)
            return string.Join(", ", parts.Skip(parts.Count - 2));

        return "Nearby service area";
    }
}
