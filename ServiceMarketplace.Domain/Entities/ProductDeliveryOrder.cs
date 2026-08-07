using ServiceMarketplace.Domain.Enums;

namespace ServiceMarketplace.Domain.Entities;

public class ProductDeliveryOrder : BaseAuditableEntity
{
    public Guid ProductListingId { get; set; }
    public ProductListing ProductListing { get; set; } = null!;

    public Guid SellerId { get; set; }
    public SellerProfile SellerProfile { get; set; } = null!;

    public string BuyerId { get; set; } = string.Empty;

    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal TotalPrice { get; set; }
    public ProductDeliveryStatus Status { get; set; } = ProductDeliveryStatus.PendingSellerConfirmation;

    public string DeliveryRecipientName { get; set; } = string.Empty;
    public string DeliveryPhoneNumber { get; set; } = string.Empty;
    public string DeliveryAddress { get; set; } = string.Empty;
    public string DeliveryCity { get; set; } = string.Empty;
    public string DeliveryState { get; set; } = string.Empty;
    public Guid? ServiceZoneId { get; set; }
    public ServiceZone? ServiceZone { get; set; }

    public string? BuyerNotes { get; set; }
    public string? CancellationReason { get; set; }
    public string? CancelledByUserId { get; set; }

    public DateTime? ConfirmedAt { get; set; }
    public DateTime? ReadyForPickupAt { get; set; }
    public DateTime? OutForDeliveryAt { get; set; }
    public DateTime? DeliveredAt { get; set; }
    public DateTime? CancelledAt { get; set; }
}
