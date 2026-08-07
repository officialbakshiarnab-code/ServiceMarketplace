using ServiceMarketplace.Application.DTOs;
using ServiceMarketplace.Domain.Enums;

namespace ServiceMarketplace.Application.Interfaces;

public interface IMarketplaceEconomicsService
{
    Task<PlatformPaymentIntentDto?> GetPlatformIntentForOrderAsync(Guid orderId, string userId);
    Task<PlatformPaymentIntentDto> CreatePlatformIntentAsync(Guid orderId, string customerId);
    Task<ServiceOrderPaymentDto> VerifyPlatformIntentAsync(Guid intentId, string adminId, VerifyPlatformPaymentIntentDto dto);
    Task<ProviderPayoutDto?> GetPayoutForOrderAsync(Guid orderId, string userId);
    Task<List<ProviderPayoutDto>> GetPayoutsAsync(ProviderPayoutStatus? status = null);
    Task<ProviderPayoutDto> MarkPayoutPaidAsync(Guid payoutId, string adminId, MarkProviderPayoutPaidDto dto);
    Task<ServiceOrderDisputeDto> CreateDisputeAsync(Guid orderId, string userId, CreateServiceOrderDisputeDto dto);
    Task<List<ServiceOrderDisputeDto>> GetDisputesForOrderAsync(Guid orderId, string userId);
    Task<List<ServiceOrderDisputeDto>> GetDisputesAsync(ServiceOrderDisputeStatus? status = null);
    Task<ServiceOrderDisputeDto> ResolveDisputeAsync(Guid disputeId, string adminId, ResolveServiceOrderDisputeDto dto);
}
