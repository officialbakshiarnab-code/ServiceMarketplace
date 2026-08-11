using ServiceMarketplace.Domain.Enums;

namespace ServiceMarketplace.Domain.Entities;

public class DeviceRegistration : BaseAuditableEntity
{
    public string UserId { get; set; } = string.Empty;
    public DevicePlatform Platform { get; set; } = DevicePlatform.Unknown;
    public string DeviceToken { get; set; } = string.Empty;
    public string? DeviceName { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime LastSeenAt { get; set; }
}
