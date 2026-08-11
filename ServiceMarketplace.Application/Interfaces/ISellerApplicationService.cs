using ServiceMarketplace.Application.DTOs;
using ServiceMarketplace.Domain.Enums;

namespace ServiceMarketplace.Application.Interfaces;

public interface ISellerApplicationService
{
    Task<SellerApplicationDto?> GetMyApplicationAsync(string userId);
    Task<SellerApplicationDto> UpsertMyApplicationAsync(string userId, UpsertSellerApplicationDto dto);
    Task<SellerApplicationDto> SubmitMyApplicationAsync(string userId);
    Task<IReadOnlyList<SellerApplicationDto>> GetForAdminAsync(SellerApplicationStatus? status);
    Task<SellerApplicationDto> ReviewAsync(Guid applicationId, string adminUserId, ReviewSellerApplicationDto dto);
}
