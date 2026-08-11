using System.Text.Json.Serialization;
using ServiceMarketplace.Domain.Enums;

namespace ServiceMarketplace.Application.DTOs;

// Purpose: Provider-facing service request details before bid acceptance.
// Excludes exact address, coordinates, customer id, and other sensitive customer data.
public class ProviderServiceRequestDto
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public Guid? ServiceCategoryId { get; set; }
    public string? ServiceCategoryName { get; set; }
    public string ApproximateLocation { get; set; } = string.Empty;
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ExactLocation { get; set; }
    public Guid? ServiceZoneId { get; set; }
    public string? ServiceZoneName { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public double? Latitude { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public double? Longitude { get; set; }
    public bool IsExactLocationVisible { get; set; }
    public double DistanceKm { get; set; }
    public int BidCount { get; set; }
    public ServiceRequestStatus Status { get; set; }
    public ServiceRequestUrgency Urgency { get; set; }
    public DateTime? PreferredStartAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
