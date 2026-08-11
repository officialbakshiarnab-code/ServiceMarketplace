using ServiceMarketplace.Application.DTOs;

namespace ServiceMarketplace.Application.Interfaces;

public interface IServicePackageService
{
    Task<List<ServicePackageDto>> GetActiveAsync(Guid? categoryId = null, Guid? zoneId = null);
    Task<List<ServicePackageDto>> GetMineAsync(string providerId);
    Task<ServicePackageDto> CreateAsync(string providerId, UpsertServicePackageDto dto);
    Task<ServicePackageDto> UpdateAsync(Guid packageId, string providerId, UpsertServicePackageDto dto);
    Task<ServiceOrderDto> BookAsync(Guid packageId, string customerId, BookServicePackageDto dto);
}
