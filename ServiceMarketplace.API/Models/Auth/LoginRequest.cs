namespace ServiceMarketplace.API.Models.Auth;

// Purpose: Login request payload for /api/auth/login.
public class LoginRequest
{
    // Identifier may be an email address or normalized phone number.
    public string Identifier { get; set; } = string.Empty;

    // Backward-compatible alias for older clients/tests that still post Email.
    public string? Email
    {
        get => Identifier;
        set => Identifier = value ?? string.Empty;
    }

    public string Password { get; set; } = string.Empty;
}
