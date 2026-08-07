using Microsoft.EntityFrameworkCore;
using ServiceMarketplace.Application.Constants;
using ServiceMarketplace.Application.DTOs;
using ServiceMarketplace.Application.Exceptions;
using ServiceMarketplace.Application.Interfaces;
using ServiceMarketplace.Domain.Entities;
using ServiceMarketplace.Domain.Enums;
using ServiceMarketplace.Infrastructure.Data;

namespace ServiceMarketplace.Infrastructure.Services;

// Purpose: Business logic for placing and listing bids.
public class BidService : IBidService
{
    private readonly AppDbContext _context;

    public BidService(AppDbContext context)
    {
        _context = context;
    }

    public async Task PlaceBidAsync(CreateBidDto dto, string providerUserId)
    {
        if (string.IsNullOrWhiteSpace(providerUserId))
            throw new UnauthorizedAccessException("Authentication is required.");

        var providerProfile = await EnsureApprovedProviderAsync(providerUserId);

        var request = await _context.ServiceRequests
            .Include(r => r.ServiceCategory)
            .Include(r => r.ServiceZone)
            .FirstOrDefaultAsync(r => r.Id == dto.ServiceRequestId);

        if (request == null)
            throw new NotFoundException("Service request not found");

        if (request.CustomerId == providerUserId)
            throw new ForbiddenException("Cannot bid on your own request");

        if (request.Status != ServiceRequestStatus.Open)
            throw new BadRequestException("Bidding is closed for this request");

        if (!CanServeRequest(providerProfile, request))
            throw new BadRequestException("Provider coverage does not match this request");

        var alreadyBid = await _context.Bids
            .AnyAsync(b => b.ServiceRequestId == dto.ServiceRequestId && b.ServiceProviderId == providerUserId);

        if (alreadyBid)
            throw new BadRequestException("You already placed a bid for this request");

        var proposedUtc = NormalizeToUtc(dto.ProposedDateTime);
        if (proposedUtc <= DateTime.UtcNow.AddMinutes(-1))
            throw new BadRequestException("Proposed date/time must be in the future");

        if (dto.EstimatedDurationMinutes is <= 0 or > 10080)
            throw new BadRequestException("Estimated duration must be between 1 minute and 7 days");

        var bid = new Bid
        {
            ServiceRequestId = dto.ServiceRequestId,
            ServiceProviderId = providerUserId,
            Amount = dto.Amount,
            ProposedDateTime = proposedUtc,
            EstimatedDurationMinutes = dto.EstimatedDurationMinutes,
            Message = dto.Message,
            Status = BidStatus.Pending
        };

        _context.Bids.Add(bid);
        await _context.SaveChangesAsync();
    }

    public async Task<IEnumerable<BidDto>> GetBidsForRequestAsync(Guid requestId, string userId)
    {
        var request = await _context.ServiceRequests
            .FirstOrDefaultAsync(r => r.Id == requestId);

        if (request == null)
            throw new NotFoundException("Service request not found");

        if (request.CustomerId != userId)
            throw new ForbiddenException("Not authorized to view bids");

        var bids = await _context.Bids
            .Where(b => b.ServiceRequestId == requestId)
            .OrderByDescending(b => b.CreatedAt)
            .ToListAsync();

        var providerIds = bids
            .Select(b => Guid.TryParse(b.ServiceProviderId, out var id) ? id : (Guid?)null)
            .Where(id => id.HasValue)
            .Select(id => id!.Value)
            .ToList();

        var providerProfiles = await _context.ServiceProviderProfiles
            .AsNoTracking()
            .Where(p => providerIds.Contains(p.UserId))
            .ToDictionaryAsync(p => p.UserId);

        var ranked = bids
            .OrderBy(b => b.Status == BidStatus.Accepted ? 0 : 1)
            .ThenBy(b => b.Amount)
            .ThenBy(b => b.ProposedDateTime)
            .ThenBy(b => b.CreatedAt)
            .Select((b, index) =>
            {
                ServiceProviderProfile? profile = null;
                if (Guid.TryParse(b.ServiceProviderId, out var providerId))
                    providerProfiles.TryGetValue(providerId, out profile);

                return new BidDto
                {
                    Id = b.Id,
                    ServiceRequestId = b.ServiceRequestId,
                    ServiceProviderId = b.ServiceProviderId,
                    ProviderDisplayName = profile?.DisplayName ?? "Service Provider",
                    ProviderBusinessName = profile?.BusinessName,
                    ProviderHourlyRate = profile?.HourlyRate,
                    ProviderAverageRating = profile?.AverageRating ?? 0m,
                    ProviderReviewCount = profile?.ReviewCount ?? 0,
                    Amount = b.Amount,
                    ProposedDateTime = b.ProposedDateTime,
                    EstimatedDurationMinutes = b.EstimatedDurationMinutes,
                    Message = b.Message,
                    Status = b.Status,
                    ComparisonRank = index + 1,
                    CreatedAt = b.CreatedAt,
                    UpdatedAt = b.UpdatedAt
                };
            })
            .ToList();

        return ranked;
    }

    public async Task<List<ProviderBidDto>> GetMyBidsAsync(string providerUserId)
    {
        if (string.IsNullOrWhiteSpace(providerUserId))
            return new List<ProviderBidDto>();

        return await _context.Bids
            .Where(b => b.ServiceProviderId == providerUserId)
            .Include(b => b.ServiceRequest)
            .ThenInclude(r => r.ServiceZone)
            .OrderByDescending(b => b.CreatedAt)
            .Select(b => new ProviderBidDto
            {
                Id = b.Id,
                ServiceRequestId = b.ServiceRequestId,
                RequestTitle = b.ServiceRequest != null ? b.ServiceRequest.Title : "Unknown Request",
                RequestCategory = b.ServiceRequest != null ? b.ServiceRequest.Category : string.Empty,
                ApproximateLocation = b.ServiceRequest != null
                    ? (b.ServiceRequest.ServiceZone != null ? b.ServiceRequest.ServiceZone.DisplayName : ToApproximateLocation(b.ServiceRequest.Location))
                    : "Nearby service area",
                ExactLocation = b.Status == BidStatus.Accepted && b.ServiceRequest != null ? b.ServiceRequest.Location : null,
                Latitude = b.Status == BidStatus.Accepted && b.ServiceRequest != null ? b.ServiceRequest.Latitude : null,
                Longitude = b.Status == BidStatus.Accepted && b.ServiceRequest != null ? b.ServiceRequest.Longitude : null,
                IsExactLocationVisible = b.Status == BidStatus.Accepted,
                Amount = b.Amount,
                ProposedDateTime = b.ProposedDateTime,
                EstimatedDurationMinutes = b.EstimatedDurationMinutes,
                Message = b.Message,
                Status = b.Status,
                CreatedAt = b.CreatedAt
            })
            .ToListAsync();
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

    private async Task<ServiceProviderProfile?> EnsureApprovedProviderAsync(string providerUserId)
    {
        if (!Guid.TryParse(providerUserId, out var providerGuid))
            throw new ForbiddenException("Provider approval is required before bidding");

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
            throw new ForbiddenException("Provider approval is required before bidding");
        }

        var approvedProfile = await _context.ServiceProviderProfiles
            .AsNoTracking()
            .Include(p => p.ServiceCategory)
            .Include(p => p.ServiceZone)
            .FirstOrDefaultAsync(p => p.UserId == providerGuid && p.Status == ProviderApplicationStatus.Approved);

        if (approvedProfile is { IsAvailable: false })
            throw new ForbiddenException("Provider availability must be enabled before bidding");

        return approvedProfile;
    }

    private static bool CanServeRequest(ServiceProviderProfile? profile, ServiceRequest request)
    {
        if (profile == null)
            return true;

        if (profile.ServiceCategoryId.HasValue && request.ServiceCategoryId.HasValue && profile.ServiceCategoryId != request.ServiceCategoryId)
            return false;

        if (!profile.ServiceCategoryId.HasValue || !request.ServiceCategoryId.HasValue)
        {
            var categoryMatches =
                string.Equals(profile.PrimaryCategory, request.Category, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(profile.PrimaryCategory, request.ServiceCategory?.Name, StringComparison.OrdinalIgnoreCase);

            if (!categoryMatches)
                return false;
        }

        if (profile.ServiceZoneId.HasValue && request.ServiceZoneId.HasValue && profile.ServiceZoneId != request.ServiceZoneId)
            return false;

        if (!profile.ServiceZoneId.HasValue || !request.ServiceZoneId.HasValue)
        {
            var providerAreaParts = new[] { profile.ServiceAreaZone, profile.ServiceAreaCity, profile.ServiceAreaState }
                .Where(part => !string.IsNullOrWhiteSpace(part))
                .ToList();

            var requestZoneName = request.ServiceZone?.DisplayName;
            var areaMatches = providerAreaParts.Any(part =>
                request.Location.Contains(part!, StringComparison.OrdinalIgnoreCase) ||
                (requestZoneName?.Contains(part!, StringComparison.OrdinalIgnoreCase) ?? false));

            if (!areaMatches)
                return false;
        }

        return true;
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
