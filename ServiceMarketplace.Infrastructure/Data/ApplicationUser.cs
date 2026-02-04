using Microsoft.AspNetCore.Identity;
using ServiceMarketplace.Domain.Enums;

namespace ServiceMarketplace.Infrastructure.Data;

/// <summary>
/// Extended IdentityUser with additional profile information.
/// Inherits all Identity functionality (password, email, roles, etc.)
/// and adds personal and verification details.
/// </summary>
public class ApplicationUser : IdentityUser
{
    /// <summary>
    /// User's first name (required).
    /// </summary>
    public string FirstName { get; set; } = null!;

    /// <summary>
    /// User's last name (required).
    /// </summary>
    public string LastName { get; set; } = null!;

    /// <summary>
    /// Primary phone number (required).
    /// Format: +1234567890 or 1234567890
    /// </summary>
    public string PhonePrimary { get; set; } = null!;

    /// <summary>
    /// Secondary phone number (optional).
    /// For backup contact purposes.
    /// </summary>
    public string? PhoneSecondary { get; set; }

    /// <summary>
    /// User's date of birth (required).
    /// Used for age verification and identity confirmation.
    /// </summary>
    public DateTime DateOfBirth { get; set; }

    /// <summary>
    /// Path or URL to government-issued ID image (optional).
    /// Used for identity verification for ServiceProvider roles.
    /// Stored as file path or cloud storage URI.
    /// </summary>
    public string? GovernmentIdImagePath { get; set; }

    /// <summary>
    /// User type enumeration (stored as INT in database).
    /// Determines which roles the user has and what actions they can perform.
    /// 
    /// Values:
    /// - 1 (User): Can create service requests and manage bids
    /// - 2 (Provider): Can browse requests and place bids
    /// - 3 (Both): Has both User and Provider capabilities
    /// </summary>
    public UserType UserType { get; set; } = UserType.User;

    /// <summary>
    /// Account creation timestamp (UTC).
    /// </summary>
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Full name derived from FirstName and LastName.
    /// </summary>
    public string FullName => $"{FirstName} {LastName}".Trim();
}
