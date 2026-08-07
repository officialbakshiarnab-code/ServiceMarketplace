namespace ServiceMarketplace.Domain.Entities;

public class ServicePackage : BaseAuditableEntity
{
    public string ProviderId { get; set; } = string.Empty;

    public Guid ServiceCategoryId { get; set; }
    public ServiceCategory ServiceCategory { get; set; } = null!;

    public Guid? ServiceZoneId { get; set; }
    public ServiceZone? ServiceZone { get; set; }

    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public int? EstimatedDurationMinutes { get; set; }
    public bool IsActive { get; set; } = true;

    public ICollection<ServiceOrder> ServiceOrders { get; set; } = new List<ServiceOrder>();
}
