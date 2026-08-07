namespace ServiceMarketplace.Domain.Entities;

public class ServiceZone : BaseAuditableEntity
{
    public string Country { get; set; } = "India";
    public string State { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string? ZoneName { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public string? PinCodeRegion { get; set; }
    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; }

    public ICollection<ServiceRequest> ServiceRequests { get; set; } = new List<ServiceRequest>();
    public ICollection<ServicePackage> ServicePackages { get; set; } = new List<ServicePackage>();
    public ICollection<ProductListing> ProductListings { get; set; } = new List<ProductListing>();
}
