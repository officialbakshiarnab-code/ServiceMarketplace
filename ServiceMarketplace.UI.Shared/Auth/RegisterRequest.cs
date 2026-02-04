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
}