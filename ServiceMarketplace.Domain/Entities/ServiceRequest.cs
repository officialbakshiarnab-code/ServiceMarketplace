using ServiceMarketplace.Domain.Enums;

namespace ServiceMarketplace.Domain.Entities;

public class ServiceRequest : BaseAuditableEntity
{
    /// <summary>
    /// FK to Users(Id) - nvarchar(450)
    /// </summary>
    public string CustomerId { get; set; } = null!;

    public string Title { get; set; } = null!;
    public string Description { get; set; } = null!;
    public string Category { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;

    public double Latitude { get; set; }
    public double Longitude { get; set; }

    public ServiceRequestStatus Status { get; set; } = ServiceRequestStatus.Open;

    public ICollection<Bid> Bids { get; set; } = new List<Bid>();
}

