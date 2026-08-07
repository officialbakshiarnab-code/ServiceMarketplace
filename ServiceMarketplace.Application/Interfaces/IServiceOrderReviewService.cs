using ServiceMarketplace.Application.DTOs;

namespace ServiceMarketplace.Application.Interfaces;

public interface IServiceOrderReviewService
{
    Task<ServiceOrderReviewDto?> GetForOrderAsync(Guid orderId, string userId);
    Task<List<ServiceOrderReviewDto>> GetForProviderAsync(string providerId);
    Task<List<ServiceOrderReviewDto>> GetForAdminAsync(bool includeHidden = true);
    Task<ServiceOrderReviewDto> CreateAsync(Guid orderId, string customerId, CreateServiceOrderReviewDto dto);
    Task<ServiceOrderReviewDto> ModerateAsync(Guid reviewId, string adminUserId, ModerateServiceOrderReviewDto dto);
}
