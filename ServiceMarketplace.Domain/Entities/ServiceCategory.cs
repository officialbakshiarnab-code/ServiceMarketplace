namespace ServiceMarketplace.Domain.Entities;

public class ServiceCategory : BaseAuditableEntity
{
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; }

    public ICollection<ServiceRequest> ServiceRequests { get; set; } = new List<ServiceRequest>();
    public ICollection<ServicePackage> ServicePackages { get; set; } = new List<ServicePackage>();
}
