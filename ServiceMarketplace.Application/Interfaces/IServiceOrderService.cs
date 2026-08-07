using ServiceMarketplace.Application.DTOs;

namespace ServiceMarketplace.Application.Interfaces;

public interface IServiceOrderService
{
    Task<List<ServiceOrderDto>> GetForCustomerAsync(string customerId);
    Task<List<ServiceOrderDto>> GetForProviderAsync(string providerId);
    Task<ServiceOrderDto> GetByIdAsync(Guid orderId, string userId);
    Task<ServiceOrderDto> StartAsync(Guid orderId, string providerId);
    Task<ServiceOrderDto> MarkProviderCompletedAsync(Guid orderId, string providerId);
    Task<ServiceOrderDto> ConfirmCustomerCompletionAsync(Guid orderId, string customerId);
    Task<ServiceOrderDto> CancelAsync(Guid orderId, string userId, CancelServiceOrderDto dto);
}
