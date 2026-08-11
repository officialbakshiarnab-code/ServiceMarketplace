using ServiceMarketplace.Application.DTOs;
using ServiceMarketplace.Domain.Enums;

namespace ServiceMarketplace.Application.Interfaces;

public interface IProfileDirectoryService
{
    Task<ProfileSearchResponse> SearchAsync(ProfileSearchRequest request);
    Task<ProfileDetailDto> GetDetailAsync(ContactProfileType profileType, string userId);
}
