namespace ServiceMarketplace.Application.DTOs;

public class ServiceCategoryDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsActive { get; set; }
    public int SortOrder { get; set; }
}

public class ServiceZoneDto
{
    public Guid Id { get; set; }
    public string Country { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string? ZoneName { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public string? PinCodeRegion { get; set; }
    public bool IsActive { get; set; }
    public int SortOrder { get; set; }
}
