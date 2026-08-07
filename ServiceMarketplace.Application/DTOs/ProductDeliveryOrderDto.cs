using ServiceMarketplace.Domain.Enums;

namespace ServiceMarketplace.Application.DTOs;

public class ProductDeliveryOrderDto
{
    public Guid Id { get; set; }
    public Guid ProductListingId { get; set; }
    public string ProductTitle { get; set; } = string.Empty;
    public string? ProductImageUrl { get; set; }
    public ProductCondition ProductCondition { get; set; }
    public string ProductConditionLabel { get; set; } = string.Empty;
    public Guid ProductCategoryId { get; set; }
    public string ProductCategoryName { get; set; } = string.Empty;
    public string SellerId { get; set; } = string.Empty;
    public string StoreName { get; set; } = string.Empty;
    public string? SellerPickupAddress { get; set; }
    public string BuyerId { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal TotalPrice { get; set; }
    public ProductDeliveryStatus Status { get; set; }
    public string StatusLabel { get; set; } = string.Empty;
    public string DeliveryRecipientName { get; set; } = string.Empty;
    public string DeliveryPhoneNumber { get; set; } = string.Empty;
    public string DeliveryAddress { get; set; } = string.Empty;
    public string DeliveryCity { get; set; } = string.Empty;
    public string DeliveryState { get; set; } = string.Empty;
    public Guid? ServiceZoneId { get; set; }
    public string? ServiceZoneName { get; set; }
    public string? BuyerNotes { get; set; }
    public string? CancellationReason { get; set; }
    public string? CancelledByUserId { get; set; }
    public DateTime? ConfirmedAt { get; set; }
    public DateTime? ReadyForPickupAt { get; set; }
    public DateTime? OutForDeliveryAt { get; set; }
    public DateTime? DeliveredAt { get; set; }
    public DateTime? CancelledAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
