namespace ServiceMarketplace.Application.DTOs;

public class ServicePackageDto
{
    public Guid Id { get; set; }
    public string ProviderId { get; set; } = string.Empty;
    public string ProviderDisplayName { get; set; } = string.Empty;
    public string? ProviderBusinessName { get; set; }
    public decimal ProviderAverageRating { get; set; }
    public int ProviderReviewCount { get; set; }
    public Guid ServiceCategoryId { get; set; }
    public string ServiceCategoryName { get; set; } = string.Empty;
    public Guid? ServiceZoneId { get; set; }
    public string? ServiceZoneName { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public int? EstimatedDurationMinutes { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
