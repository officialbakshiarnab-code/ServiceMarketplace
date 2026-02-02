using ServiceMarketplace.Domain.Enums;

namespace ServiceMarketplace.Application.DTOs;

// Purpose: Service request details returned to clients.
public class ServiceRequestDto
{
    public Guid Id { get; set; }
    public string CustomerId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public double DistanceKm { get; set; }
    public int BidCount { get; set; }
    public ServiceRequestStatus Status { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
