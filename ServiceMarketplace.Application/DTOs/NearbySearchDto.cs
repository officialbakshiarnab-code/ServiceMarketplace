namespace ServiceMarketplace.Application.DTOs;

// Purpose: Search parameters for nearby open service requests.
public class NearbySearchDto
{
    public double Latitude { get; set; }

    public double Longitude { get; set; }

    public double RadiusKm { get; set; }
}
