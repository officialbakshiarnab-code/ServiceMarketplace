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

    /// <summary>
    /// Reserved for the multipart registration flow.
    /// </summary>
    public IFormFile? GovernmentIdImage { get; set; }
}
