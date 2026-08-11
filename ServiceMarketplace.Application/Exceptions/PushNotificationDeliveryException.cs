namespace ServiceMarketplace.Application.Exceptions;

public sealed class PushNotificationDeliveryException(
    string message,
    bool shouldDeactivateDevice = false,
    Exception? innerException = null) : Exception(message, innerException)
{
    public bool ShouldDeactivateDevice { get; } = shouldDeactivateDevice;
}
