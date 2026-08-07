using ServiceMarketplace.Application.DTOs;
using ServiceMarketplace.Domain.Enums;

namespace ServiceMarketplace.Application.Interfaces;

public interface IProviderApplicationService
{
    Task<ProviderApplicationDto?> GetMyApplicationAsync(string userId);
    Task<ProviderApplicationDto> UpsertMyApplicationAsync(string userId, UpsertProviderApplicationDto dto);
    Task<ProviderApplicationDto> SubmitMyApplicationAsync(string userId);
    Task<IReadOnlyList<ProviderApplicationDto>> GetForAdminAsync(ProviderApplicationStatus? status);
    Task<ProviderApplicationDto> ReviewAsync(Guid applicationId, string adminUserId, ReviewProviderApplicationDto dto);
    Task<string?> GetApprovedServiceAreaLabelAsync(string providerUserId);
}
