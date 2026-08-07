namespace ServiceMarketplace.Application.DTOs;

public class BookServicePackageDto
{
    public DateTime ScheduledStartAt { get; set; }
    public string Location { get; set; } = string.Empty;
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public string? Requirements { get; set; }
}
