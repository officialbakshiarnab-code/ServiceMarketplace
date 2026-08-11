using ServiceMarketplace.Domain.Enums;

namespace ServiceMarketplace.Application.DTOs;

// Purpose: Input payload when a user creates a service request.
public class CreateServiceRequestDto
{
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public Guid? ServiceCategoryId { get; set; }
    public string Location { get; set; } = string.Empty;
    public Guid? ServiceZoneId { get; set; }
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public ServiceRequestUrgency Urgency { get; set; } = ServiceRequestUrgency.Flexible;
    public DateTime? PreferredStartAt { get; set; }
    public string? Requirements { get; set; }
}
