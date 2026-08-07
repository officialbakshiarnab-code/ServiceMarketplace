namespace ServiceMarketplace.Application.DTOs;

public class CreateProductDeliveryOrderDto
{
    public Guid ProductListingId { get; set; }
    public int Quantity { get; set; } = 1;
    public string DeliveryRecipientName { get; set; } = string.Empty;
    public string DeliveryPhoneNumber { get; set; } = string.Empty;
    public string DeliveryAddress { get; set; } = string.Empty;
    public string DeliveryCity { get; set; } = string.Empty;
    public string DeliveryState { get; set; } = string.Empty;
    public Guid? ServiceZoneId { get; set; }
    public string? BuyerNotes { get; set; }
}
