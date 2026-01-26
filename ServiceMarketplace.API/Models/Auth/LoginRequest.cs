namespace ServiceMarketplace.API.Models.Auth;

// Purpose: Login request payload for /api/auth/login.
public class LoginRequest
{
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}
