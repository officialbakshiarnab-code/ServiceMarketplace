namespace ServiceMarketplace.API.Models.Auth;

// Purpose: Registration request payload for /api/auth/register.
public class RegisterRequest
{
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
}
