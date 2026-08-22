using ServiceMarketplace.Application.DTOs;

namespace ServiceMarketplace.Application.Interfaces;

public interface IPushNotificationSender
{
    Task SendAsync(string deviceToken, PushNotificationPayloadDto payload, CancellationToken cancellationToken = default);
}
