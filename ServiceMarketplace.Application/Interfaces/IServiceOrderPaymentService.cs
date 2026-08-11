using ServiceMarketplace.Application.DTOs;

namespace ServiceMarketplace.Application.Interfaces;

public interface IServiceOrderPaymentService
{
    Task<ServiceOrderPaymentDto?> GetForOrderAsync(Guid orderId, string userId);
    Task<ServiceOrderPaymentDto> RecordAsync(Guid orderId, string customerId, RecordServiceOrderPaymentDto dto);
}
