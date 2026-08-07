using ServiceMarketplace.Application.DTOs;

namespace ServiceMarketplace.Application.Interfaces;

public interface IProductDeliveryOrderService
{
    Task<ProductDeliveryOrderDto> CreateAsync(string buyerId, CreateProductDeliveryOrderDto dto);
    Task<List<ProductDeliveryOrderDto>> GetMineAsBuyerAsync(string buyerId);
    Task<List<ProductDeliveryOrderDto>> GetMineAsSellerAsync(string sellerId);
    Task<ProductDeliveryOrderDto> UpdateSellerStatusAsync(Guid orderId, string sellerId, UpdateProductDeliveryStatusDto dto);
    Task<ProductDeliveryOrderDto> CancelAsBuyerAsync(Guid orderId, string buyerId, string? cancellationReason);
}
