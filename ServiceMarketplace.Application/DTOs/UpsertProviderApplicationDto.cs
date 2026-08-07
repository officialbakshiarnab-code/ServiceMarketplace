namespace ServiceMarketplace.Application.DTOs;

public class UpsertProviderApplicationDto
{
    public string DisplayName { get; set; } = string.Empty;
    public string? BusinessName { get; set; }
    public string? Bio { get; set; }
    public string Skills { get; set; } = string.Empty;
    public string PrimaryCategory { get; set; } = string.Empty;
    public Guid? ServiceCategoryId { get; set; }
    public string ServiceAreaCity { get; set; } = string.Empty;
    public string ServiceAreaState { get; set; } = string.Empty;
    public string? ServiceAreaZone { get; set; }
    public Guid? ServiceZoneId { get; set; }
    public decimal HourlyRate { get; set; }
    public bool IsAvailable { get; set; } = true;
    public bool IdentityVerificationSubmitted { get; set; }
    public bool AddressVerificationSubmitted { get; set; }
    public bool BackgroundCheckConsent { get; set; }
}
