using ServiceMarketplace.Application.DTOs;

namespace ServiceMarketplace.Application.Interfaces;

public interface IDeviceRegistrationService
{
    Task<DeviceRegistrationDto> RegisterAsync(string userId, RegisterDeviceDto request);
    Task<List<DeviceRegistrationDto>> GetMineAsync(string userId);
    Task DeactivateAsync(Guid registrationId, string userId);
}
