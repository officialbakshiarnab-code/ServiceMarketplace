using ServiceMarketplace.Domain.Enums;

namespace ServiceMarketplace.Application.DTOs;

public sealed class RegisterDeviceDto
{
    public DevicePlatform Platform { get; set; } = DevicePlatform.Unknown;
    public string DeviceToken { get; set; } = string.Empty;
    public string? DeviceName { get; set; }
}

public sealed class DeviceRegistrationDto
{
    public Guid Id { get; set; }
    public DevicePlatform Platform { get; set; }
    public string DeviceName { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public DateTime LastSeenAt { get; set; }
    public DateTime CreatedAt { get; set; }
}

public sealed class PushNotificationPayloadDto
{
    public string Type { get; set; } = string.Empty;
    public Guid? ConversationId { get; set; }
    public Guid? MessageId { get; set; }
}
