using ServiceMarketplace.Application.DTOs;

namespace ServiceMarketplace.Application.Interfaces;

// Purpose: Application contract for service request use cases.
public interface IServiceRequestService
{
    Task<Guid> CreateAsync(CreateServiceRequestDto dto, string userId);
    Task<IEnumerable<ServiceRequestDto>> GetOpenAsync();
    Task<List<ServiceRequestDto>> GetNearbyAsync(double latitude, double longitude, double radiusKm);
    Task<List<ServiceRequestDto>> GetMyRequestsAsync(string userId);
    Task<List<ServiceRequestDto>> GetAvailableForProviderAsync(string providerUserId);
    Task<ServiceRequestDto> GetByIdForUserAsync(Guid requestId, string userId);
    Task<ServiceRequestDto> GetByIdForProviderAsync(Guid requestId, string providerId);
    Task AcceptBidAsync(Guid requestId, Guid bidId, string userId);
    
    /// <summary>
    /// Get dashboard statistics for the current user.
    /// Includes counts of open requests, active bids, and completed requests.
    /// </summary>
    /// <param name="userId">User ID to get stats for</param>
    /// <returns>Dashboard statistics</returns>
    Task<UserDashboardStatsDto> GetDashboardStatsAsync(string userId);
}
