namespace ServiceMarketplace.Domain.Enums;

public enum PlatformPaymentIntentStatus : short
{
    PendingVerification = 1,
    Verified = 2,
    Failed = 3,
    Cancelled = 4
}
