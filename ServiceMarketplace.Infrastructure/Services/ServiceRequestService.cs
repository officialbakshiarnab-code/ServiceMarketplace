using Microsoft.EntityFrameworkCore;
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

    public ServiceRequestService(AppDbContext context, INotificationService notificationService)
    {
        _context = context;
        _notificationService = notificationService;
    }

    public async Task<Guid> CreateAsync(CreateServiceRequestDto dto, string userId)
    {
        var request = new ServiceRequest
        {
            CustomerId = userId,
            Title = dto.Title,
            Description = dto.Description,
            Latitude = dto.Latitude,
            Longitude = dto.Longitude,
            Status = ServiceRequestStatus.Open
        };

        _context.ServiceRequests.Add(request);
        await _context.SaveChangesAsync();

        await _notificationService.NotifyNewRequestNearbyAsync(request.Id);

        return request.Id;
    }

    public async Task<IEnumerable<ServiceRequestDto>> GetOpenAsync()
    {
        return await _context.ServiceRequests
            .Where(r => r.Status == ServiceRequestStatus.Open)
            .Select(r => new ServiceRequestDto
            {
                Id = r.Id,
                CustomerId = r.CustomerId,
                Title = r.Title,
                Description = r.Description,
                Latitude = r.Latitude,
                Longitude = r.Longitude,
                Status = r.Status
            })
            .ToListAsync();
    }

    public async Task<List<ServiceRequestDto>> GetNearbyAsync(double latitude, double longitude, double radiusKm)
    {
        const double EarthRadiusKm = 6371.0;
        var latRad = latitude * (Math.PI / 180.0);
        var lonRad = longitude * (Math.PI / 180.0);

        return await _context.ServiceRequests
            .Where(r => r.Status == ServiceRequestStatus.Open)
            .Select(r => new
            {
                Request = r,
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
            .Select(x => new ServiceRequestDto
            {
                Id = x.Request.Id,
                CustomerId = x.Request.CustomerId,
                Title = x.Request.Title,
                Description = x.Request.Description,
                Latitude = x.Request.Latitude,
                Longitude = x.Request.Longitude,
                Status = x.Request.Status,
                DistanceKm = x.DistanceKm
            })
            .ToListAsync();
    }

    public async Task AcceptBidAsync(Guid requestId, Guid bidId, string userId)
    {
        var request = await _context.ServiceRequests
            .Include(r => r.Bids)
            .FirstOrDefaultAsync(r => r.Id == requestId);

        if (request == null)
            throw new NotFoundException("Service request not found");

        if (request.CustomerId != userId)
            throw new ForbiddenException("Not authorized to accept bids for this request");

        if (request.Status != ServiceRequestStatus.Open)
            throw new Exception("Request is not open");

        var selectedBid = request.Bids.FirstOrDefault(b => b.Id == bidId);
        if (selectedBid == null)
            throw new NotFoundException("Bid not found");

        // Accept selected bid
        selectedBid.Status = BidStatus.Accepted;

        // Reject all others
        foreach (var bid in request.Bids.Where(b => b.Id != bidId))
            bid.Status = BidStatus.Rejected;

        request.Status = ServiceRequestStatus.Accepted;

        await _context.SaveChangesAsync();

        await _notificationService.NotifyBidAcceptedAsync(selectedBid.Id);
    }
}
