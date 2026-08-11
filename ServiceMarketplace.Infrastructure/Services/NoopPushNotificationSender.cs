using Microsoft.Extensions.Logging;
using ServiceMarketplace.Application.DTOs;
using ServiceMarketplace.Application.Interfaces;

namespace ServiceMarketplace.Infrastructure.Services;

public sealed class NoopPushNotificationSender(ILogger<NoopPushNotificationSender> logger) : IPushNotificationSender
{
    public Task SendAsync(string deviceToken, PushNotificationPayloadDto payload, CancellationToken cancellationToken = default)
    {
        logger.LogInformation(
            "Push notification suppressed by noop sender. Type: {Type}, ConversationId: {ConversationId}, MessageId: {MessageId}",
            payload.Type,
            payload.ConversationId,
            payload.MessageId);

        return Task.CompletedTask;
    }
}
