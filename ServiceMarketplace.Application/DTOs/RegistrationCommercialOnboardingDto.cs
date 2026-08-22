namespace ServiceMarketplace.Application.DTOs;

public sealed class RegistrationCommercialOnboardingDto
{
    public bool WantsProvider { get; set; }
    public bool WantsSeller { get; set; }
    public RegistrationBusinessDetailsDto? Business { get; set; }
    public RegistrationProviderDetailsDto? Provider { get; set; }
    public RegistrationSellerDetailsDto? Seller { get; set; }
}

public sealed class RegistrationBusinessDetailsDto
{
    public string? LegalName { get; set; }
    public string? TradingName { get; set; }
    public string? BusinessType { get; set; }
    public string? Gstin { get; set; }
    public string? WebsiteOrDomain { get; set; }
    public string? RegisteredAddress { get; set; }
    public string? OperatingAddress { get; set; }
    public int RequestedSeatLimit { get; set; } = 5;
}

public sealed class RegistrationProviderDetailsDto
{
    public string ProviderType { get; set; } = "Individual";
    public string Skills { get; set; } = string.Empty;
    public string Profession { get; set; } = string.Empty;
    public int? YearsOfExperience { get; set; }
    public string PrimaryCategory { get; set; } = string.Empty;
    public string ServiceAreaCity { get; set; } = string.Empty;
    public string ServiceAreaState { get; set; } = string.Empty;
    public string? ServiceAreaZone { get; set; }
    public string PricingType { get; set; } = "StartingPrice";
    public decimal Rate { get; set; }
    public string? Availability { get; set; }
    public string? Languages { get; set; }
}

public sealed class RegistrationSellerDetailsDto
{
    public string StoreName { get; set; } = string.Empty;
    public string? BusinessName { get; set; }
    public string? Gstin { get; set; }
    public string? ProductCategories { get; set; }
    public string ProductConditionFocus { get; set; } = "Both";
    public string PickupAddress { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
    public string? Description { get; set; }
}
