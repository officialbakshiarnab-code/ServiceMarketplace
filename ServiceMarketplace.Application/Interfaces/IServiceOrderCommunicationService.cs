using ServiceMarketplace.Application.DTOs;

namespace ServiceMarketplace.Application.Interfaces;

public interface IServiceOrderCommunicationService
{
    Task<List<ServiceOrderMessageDto>> GetMessagesAsync(Guid orderId, string userId);
    Task<ServiceOrderMessageDto> SendMessageAsync(Guid orderId, string senderUserId, CreateServiceOrderMessageDto dto);
    Task<int> MarkThreadReadAsync(Guid orderId, string userId);
}
