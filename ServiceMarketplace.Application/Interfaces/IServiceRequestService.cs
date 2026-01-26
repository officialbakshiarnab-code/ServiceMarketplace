using ServiceMarketplace.Application.DTOs;

namespace ServiceMarketplace.Application.Interfaces;

// Purpose: Application contract for service request use cases.
public interface IServiceRequestService
{
    Task<Guid> CreateAsync(CreateServiceRequestDto dto, string userId);
    Task<IEnumerable<ServiceRequestDto>> GetOpenAsync();
    Task<List<ServiceRequestDto>> GetNearbyAsync(double latitude, double longitude, double radiusKm);
    Task AcceptBidAsync(Guid requestId, Guid bidId, string userId);
}
