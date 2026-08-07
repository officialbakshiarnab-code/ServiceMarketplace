using ServiceMarketplace.Domain.Enums;

namespace ServiceMarketplace.Application.DTOs;

public class ProviderApplicationDto
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public string? BusinessName { get; set; }
    public string? Bio { get; set; }
    public string Skills { get; set; } = string.Empty;
    public string PrimaryCategory { get; set; } = string.Empty;
    public Guid? ServiceCategoryId { get; set; }
    public string? ServiceCategoryName { get; set; }
    public string ServiceAreaCity { get; set; } = string.Empty;
    public string ServiceAreaState { get; set; } = string.Empty;
    public string? ServiceAreaZone { get; set; }
    public Guid? ServiceZoneId { get; set; }
    public string? ServiceZoneName { get; set; }
    public decimal HourlyRate { get; set; }
    public bool IsAvailable { get; set; }
    public ProviderApplicationStatus Status { get; set; }
    public bool IdentityVerificationSubmitted { get; set; }
    public bool AddressVerificationSubmitted { get; set; }
    public bool BackgroundCheckConsent { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public DateTime? SubmittedAt { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public string? ReviewedByUserId { get; set; }
    public string? ReviewNotes { get; set; }
    public string? RejectionReason { get; set; }
}
