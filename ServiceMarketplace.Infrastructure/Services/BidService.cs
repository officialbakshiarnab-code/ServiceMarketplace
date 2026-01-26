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
        var request = await _context.ServiceRequests
            .FirstOrDefaultAsync(r => r.Id == dto.ServiceRequestId);

        if (request == null)
            throw new NotFoundException("Service request not found");

        if (request.Status != ServiceRequestStatus.Open)
            throw new Exception("Bidding is closed for this request");

        var bid = new Bid
        {
            ServiceRequestId = dto.ServiceRequestId,
            ServiceProviderId = providerUserId,
            Amount = dto.Amount,
            ProposedDateTime = dto.ProposedDateTime,
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
            .Select(b => new BidDto
            {
                Id = b.Id,
                ServiceRequestId = b.ServiceRequestId,
                ServiceProviderId = b.ServiceProviderId,
                Amount = b.Amount,
                ProposedDateTime = b.ProposedDateTime,
                Status = b.Status
            })
            .ToListAsync();
    }
}
