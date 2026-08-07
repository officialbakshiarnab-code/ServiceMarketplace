namespace ServiceMarketplace.Domain.Enums;

public enum UserNotificationType
{
    NewRequestNearby = 1,
    BidAccepted = 2,
    ServiceOrderCreated = 3,
    ServiceOrderStarted = 4,
    ServiceOrderProviderCompleted = 5,
    ServiceOrderCompleted = 6,
    ServiceOrderCancelled = 7,
    OrderMessageReceived = 8,
    ServiceOrderPaymentRecorded = 9,
    ServiceOrderReviewReceived = 10
}
