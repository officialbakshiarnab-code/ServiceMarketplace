namespace ServiceMarketplace.UI.Shared.Auth;

/// <summary>
/// Registration request DTO matching API contract at POST /api/auth/register.
/// Property names and types must match API's ServiceMarketplace.API.Models.Auth.RegisterRequest exactly.
/// 
/// Age Requirements:
/// - User role: No age restriction
/// - ServiceProvider role: Must be 18+ years old
/// 
/// Government ID Upload:
/// - Optional multipart file
/// - Image only (JPEG, PNG, GIF, WebP)
/// - Max 5MB
/// - File upload failure does not block registration
/// </summary>
public sealed class RegisterRequest
{
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public DateTime DateOfBirth { get; set; }
    public string Role { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public RegistrationCommercialOnboardingRequest? CommercialOnboarding { get; set; }
}

public sealed class RegistrationCommercialOnboardingRequest
{
    public bool WantsProvider { get; set; }
    public bool WantsSeller { get; set; }
    public RegistrationBusinessDetailsRequest? Business { get; set; }
    public RegistrationProviderDetailsRequest? Provider { get; set; }
    public RegistrationSellerDetailsRequest? Seller { get; set; }
}

public sealed class RegistrationBusinessDetailsRequest
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

public sealed class RegistrationProviderDetailsRequest
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

public sealed class RegistrationSellerDetailsRequest
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
