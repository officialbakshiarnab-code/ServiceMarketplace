namespace ServiceMarketplace.UI.Shared.Auth;

/// <summary>
/// Registration request DTO matching API contract at POST /api/auth/register.
/// Property names and types must match API's ServiceMarketplace.API.Models.Auth.RegisterRequest exactly.
/// </summary>
public sealed class RegisterRequest
{
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
}