namespace ServiceMarketplace.Application.DTOs;

public class NearbySearchDto
{
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public double RadiusKm { get; set; } = 5; // default 5 km
}
