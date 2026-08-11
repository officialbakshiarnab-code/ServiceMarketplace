namespace ServiceMarketplace.Domain.Enums;

public enum ProductDeliveryStatus : short
{
    PendingSellerConfirmation = 1,
    Confirmed = 2,
    ReadyForPickup = 3,
    OutForDelivery = 4,
    Delivered = 5,
    Cancelled = 6
}
