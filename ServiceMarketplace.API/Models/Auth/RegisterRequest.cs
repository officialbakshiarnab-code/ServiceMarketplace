using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace ServiceMarketplace.API.Models.Auth;

/// <summary>
/// Registration request payload for /api/auth/register.
/// Supports both form-encoded and multipart/form-data.
/// Includes all required fields for user account creation and age verification.
/// 
/// Age Requirements:
/// - User role (1): No age restriction
/// - ServiceProvider role (2): Must be 18 or older
/// - Both role (3): Must be 18 or older
/// 
/// File Upload:
/// - GovernmentIdImage: Optional (multipart/form-data only)
/// - Supported formats: JPEG, PNG, GIF, WebP
/// - Max size: 5MB
/// - Registration succeeds even if file upload fails
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
    /// User's password (plain text, hashed by Identity).
    /// Must meet ASP.NET Identity password requirements:
    /// - Minimum 6 characters
    /// - Must contain uppercase letter
    /// - Must contain lowercase letter
    /// - Must contain digit
    /// - Must contain special character
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
    /// Used to verify age requirements:
    /// - ServiceProvider and Both roles require age >= 18
    /// </summary>
    [Required(ErrorMessage = "Date of birth is required")]
    public DateTime DateOfBirth { get; set; }

    /// <summary>
    /// User role for determining permissions.
    /// Valid values: "User", "ServiceProvider", "Both"
    /// 
    /// Age restrictions:
    /// - "User": No age restriction
    /// - "ServiceProvider": Must be 18+ years old
    /// - "Both": Must be 18+ years old
    /// </summary>
    [Required(ErrorMessage = "User type is required")]
    public string Role { get; set; } = string.Empty;

    /// <summary>
    /// Optional government-issued ID image file.
    /// Only supported when using multipart/form-data.
    /// 
    /// Validation:
    /// - Image only (JPEG, PNG, GIF, WebP)
    /// - Max 5MB
    /// - File is optional - registration succeeds even if upload fails
    /// 
    /// Usage:
    /// - POST /api/auth/register (multipart/form-data)
    /// - Field name: "GovernmentIdImage"
    /// - Upload fails silently if not provided or invalid
    /// </summary>
    public IFormFile? GovernmentIdImage { get; set; }
}
