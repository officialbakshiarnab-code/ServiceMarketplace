namespace ServiceMarketplace.UI.Shared.Auth;

/// <summary>
/// Login response containing access and refresh tokens.
/// </summary>
public class AuthResponse
{
    /// <summary>
    /// JWT access token (10-minute lifetime).
    /// </summary>
    public string Token { get; set; } = string.Empty;

    /// <summary>
    /// User role from JWT claims.
    /// </summary>
    public string Role { get; set; } = string.Empty;

    /// <summary>
    /// Refresh token for obtaining new access tokens (7-day lifetime).
    /// </summary>
    public string? RefreshToken { get; set; }

    /// <summary>
    /// When the refresh token expires (UTC).
    /// </summary>
    public DateTime? RefreshTokenExpiresAt { get; set; }
}
