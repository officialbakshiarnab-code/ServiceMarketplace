using Microsoft.EntityFrameworkCore;
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

        var request = await _context.ServiceRequests
            .FirstOrDefaultAsync(r => r.Id == dto.ServiceRequestId);

        if (request == null)
            throw new NotFoundException("Service request not found");

        if (request.CustomerId == providerUserId)
            throw new ForbiddenException("Cannot bid on your own request");

        if (request.Status != ServiceRequestStatus.Open)
            throw new BadRequestException("Bidding is closed for this request");

        var alreadyBid = await _context.Bids
            .AnyAsync(b => b.ServiceRequestId == dto.ServiceRequestId && b.ServiceProviderId == providerUserId);

        if (alreadyBid)
            throw new BadRequestException("You already placed a bid for this request");

        var proposedUtc = NormalizeToUtc(dto.ProposedDateTime);

        var bid = new Bid
        {
            ServiceRequestId = dto.ServiceRequestId,
            ServiceProviderId = providerUserId,
            Amount = dto.Amount,
            ProposedDateTime = proposedUtc,
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

        return await _context.Bids
            .Where(b => b.ServiceRequestId == requestId)
            .OrderByDescending(b => b.CreatedAt)
            .Select(b => new BidDto
            {
                Id = b.Id,
                ServiceRequestId = b.ServiceRequestId,
                ServiceProviderId = b.ServiceProviderId,
                Amount = b.Amount,
                ProposedDateTime = b.ProposedDateTime,
                Message = b.Message,
                Status = b.Status,
                CreatedAt = b.CreatedAt,
                UpdatedAt = b.UpdatedAt
            })
            .ToListAsync();
    }

    public async Task<List<ProviderBidDto>> GetMyBidsAsync(string providerUserId)
    {
        if (string.IsNullOrWhiteSpace(providerUserId))
            return new List<ProviderBidDto>();

        return await _context.Bids
            .Where(b => b.ServiceProviderId == providerUserId)
            .Include(b => b.ServiceRequest)
            .OrderByDescending(b => b.CreatedAt)
            .Select(b => new ProviderBidDto
            {
                Id = b.Id,
                ServiceRequestId = b.ServiceRequestId,
                RequestTitle = b.ServiceRequest != null ? b.ServiceRequest.Title : "Unknown Request",
                Amount = b.Amount,
                ProposedDateTime = b.ProposedDateTime,
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
}
