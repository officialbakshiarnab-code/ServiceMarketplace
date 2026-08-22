using System.Collections.Concurrent;
using ServiceMarketplace.Application.DTOs;
using ServiceMarketplace.Application.Exceptions;
using ServiceMarketplace.Application.Interfaces;

namespace ServiceMarketplace.API.Tests.Fixtures;

public sealed class RecordingPushNotificationSender : IPushNotificationSender
{
    public static ConcurrentBag<PushNotificationRecord> Sent { get; } = new();
    public static ConcurrentBag<string> InvalidTokens { get; } = new();
    public static bool ThrowOnSend { get; set; }

    public static void Reset()
    {
        while (Sent.TryTake(out _))
        {
        }

        while (InvalidTokens.TryTake(out _))
        {
        }

        ThrowOnSend = false;
    }

    public Task SendAsync(string deviceToken, PushNotificationPayloadDto payload, CancellationToken cancellationToken = default)
    {
        if (ThrowOnSend)
            throw new InvalidOperationException("Simulated push delivery failure.");

        if (InvalidTokens.Contains(deviceToken))
            throw new PushNotificationDeliveryException("Simulated invalid device token.", shouldDeactivateDevice: true);

        Sent.Add(new PushNotificationRecord(deviceToken, payload));
        return Task.CompletedTask;
    }
}

public sealed record PushNotificationRecord(string DeviceToken, PushNotificationPayloadDto Payload);
