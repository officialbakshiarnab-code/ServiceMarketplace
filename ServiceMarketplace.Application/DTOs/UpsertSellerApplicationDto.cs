namespace ServiceMarketplace.Application.DTOs;

public class UpsertSellerApplicationDto
{
    public string StoreName { get; set; } = string.Empty;
    public string? BusinessName { get; set; }
    public string? Description { get; set; }
    public string? Gstin { get; set; }
    public string PickupAddress { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
    public Guid? ServiceZoneId { get; set; }
    public bool IdentityVerificationSubmitted { get; set; }
    public bool AddressVerificationSubmitted { get; set; }
    public bool BusinessVerificationSubmitted { get; set; }
}
