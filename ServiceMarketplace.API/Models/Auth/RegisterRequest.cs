using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace ServiceMarketplace.API.Models.Auth;

/// <summary>
/// Registration request payload for /api/auth/register.
/// The API currently accepts JSON for account creation.
/// </summary>
public class RegisterRequest
{
    /// <summary>
    /// User's email address (unique identifier).
    /// Must be valid email format.
    /// </summary>
    [Required(ErrorMessage = "Email is required")]
    [EmailAddress(ErrorMessage = "Invalid email format")]
    public string Email { get; set; } = string.Empty;

    /// <summary>
    /// User's password, hashed before storage.
    /// </summary>
    [Required(ErrorMessage = "Password is required")]
    [MinLength(6, ErrorMessage = "Password must be at least 6 characters")]
    public string Password { get; set; } = string.Empty;

    /// <summary>
    /// User's first name.
    /// Required for all roles.
    /// </summary>
    [Required(ErrorMessage = "First name is required")]
    [StringLength(100, ErrorMessage = "First name cannot exceed 100 characters")]
    public string FirstName { get; set; } = string.Empty;

    /// <summary>
    /// User's last name.
    /// Required for all roles.
    /// </summary>
    [Required(ErrorMessage = "Last name is required")]
    [StringLength(100, ErrorMessage = "Last name cannot exceed 100 characters")]
    public string LastName { get; set; } = string.Empty;

    /// <summary>
    /// User's date of birth.
    /// Required for all roles.
    /// Used to verify provider age requirements.
    /// </summary>
    [Required(ErrorMessage = "Date of birth is required")]
    public DateTime DateOfBirth { get; set; }

    /// <summary>
    /// User role for determining permissions.
    /// Valid values: "User", "ServiceProvider", "Both".
    /// </summary>
    [Required(ErrorMessage = "User type is required")]
    public string Role { get; set; } = string.Empty;

    [Required(ErrorMessage = "Phone number is required")]
    [StringLength(20, ErrorMessage = "Phone number cannot exceed 20 characters")]
    public string PhoneNumber { get; set; } = string.Empty;

    public RegistrationCommercialOnboardingRequest? CommercialOnboarding { get; set; }

    /// <summary>
    /// Reserved for the multipart registration flow.
    /// </summary>
    public IFormFile? GovernmentIdImage { get; set; }
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
