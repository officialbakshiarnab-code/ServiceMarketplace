namespace ServiceMarketplace.UI.Shared.Auth;

/// <summary>
/// Login request DTO matching API contract at POST /api/auth/login.
/// Property names and types must match API's ServiceMarketplace.API.Models.Auth.LoginRequest exactly.
/// </summary>
public sealed class LoginRequest
{
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}
