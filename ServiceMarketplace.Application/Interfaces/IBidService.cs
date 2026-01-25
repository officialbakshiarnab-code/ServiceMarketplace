using ServiceMarketplace.Application.DTOs;

namespace ServiceMarketplace.Application.Interfaces;

public interface IBidService
{
    Task PlaceBidAsync(CreateBidDto dto, string providerUserId);
    Task<IEnumerable<BidDto>> GetBidsForRequestAsync(Guid requestId, string userId);
}
