namespace ServiceMarketplace.Application.DTOs;

public class UpsertServicePackageDto
{
    public Guid ServiceCategoryId { get; set; }
    public Guid? ServiceZoneId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public int? EstimatedDurationMinutes { get; set; }
    public bool IsActive { get; set; } = true;
}
