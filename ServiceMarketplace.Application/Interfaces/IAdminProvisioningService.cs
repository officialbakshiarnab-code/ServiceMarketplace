using ServiceMarketplace.Application.DTOs;

namespace ServiceMarketplace.Application.Interfaces;

public interface IAdminProvisioningService
{
    Task<AdminProvisioningResult> ProvisionAsync(AdminProvisioningRequest request, CancellationToken cancellationToken = default);
}
