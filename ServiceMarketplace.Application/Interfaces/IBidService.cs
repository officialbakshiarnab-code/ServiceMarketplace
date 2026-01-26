using ServiceMarketplace.Application.DTOs;

namespace ServiceMarketplace.Application.Interfaces;

// Purpose: Application contract for bid-related use cases.
public interface IBidService
{
    Task PlaceBidAsync(CreateBidDto dto, string providerUserId);
    Task<IEnumerable<BidDto>> GetBidsForRequestAsync(Guid requestId, string userId);
}
