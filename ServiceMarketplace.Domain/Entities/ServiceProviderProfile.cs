using ServiceMarketplace.Domain.Enums;

namespace ServiceMarketplace.Domain.Entities;

public class ServiceProviderProfile : BaseAuditableEntity
{
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;

    public string DisplayName { get; set; } = string.Empty;
    public string? BusinessName { get; set; }
    public string? Bio { get; set; }
    public string Skills { get; set; } = string.Empty;
    public string PrimaryCategory { get; set; } = string.Empty;
    public Guid? ServiceCategoryId { get; set; }
    public ServiceCategory? ServiceCategory { get; set; }
    public string ServiceAreaCity { get; set; } = string.Empty;
    public string ServiceAreaState { get; set; } = string.Empty;
    public string? ServiceAreaZone { get; set; }
    public Guid? ServiceZoneId { get; set; }
    public ServiceZone? ServiceZone { get; set; }
    public decimal HourlyRate { get; set; }
    public bool IsAvailable { get; set; } = true;
    public ProviderApplicationStatus Status { get; set; } = ProviderApplicationStatus.Draft;
    public bool IdentityVerificationSubmitted { get; set; }
    public bool AddressVerificationSubmitted { get; set; }
    public bool BackgroundCheckConsent { get; set; }
    public DateTime? SubmittedAt { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public string? ReviewedByUserId { get; set; }
    public string? ReviewNotes { get; set; }
    public string? RejectionReason { get; set; }
    public decimal AverageRating { get; set; }
    public int ReviewCount { get; set; }
}
