namespace ServiceMarketplace.API.Models.Auth;

// Purpose: Authentication response with JWT and expiry.
public class AuthResponse
{
    public string Token { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
}
