using ServiceMarketplace.Application.DTOs;
using ServiceMarketplace.Domain.Enums;

namespace ServiceMarketplace.Application.Interfaces;

public interface IContactRequestService
{
    Task<ContactRequestDto> CreateAsync(string requesterUserId, CreateContactRequestDto request);
    Task<List<ContactRequestDto>> GetSentAsync(string userId);
    Task<List<ContactRequestDto>> GetReceivedAsync(string userId);
    Task<List<ContactRequestDto>> GetAdminQueueAsync(ContactRequestStatus? status);
    Task<ContactRequestDto> GetAsync(Guid id, string userId, bool isAdmin = false);
    Task<ContactRequestDto> ReviewAsync(Guid id, string adminUserId, ReviewContactRequestDto request);
    Task<ContactRequestDto> CancelAsync(Guid id, string userId);
    Task<ContactRequestDto> CompleteAsync(Guid id, string userId);
    Task<ContactRequestSummaryDto> GetSummaryAsync(string userId);
}
