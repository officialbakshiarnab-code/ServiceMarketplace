using ServiceMarketplace.Application.DTOs;

namespace ServiceMarketplace.API.Models.Auth;

/// <summary>
/// Login response containing access token and optional refresh token.
/// Returned by POST /api/auth/login endpoint.
/// </summary>
public class AuthResponse
{
    /// <summary>
    /// JWT access token (10-minute lifetime).
    /// Include in Authorization header: "Bearer {Token}"
    /// </summary>
    public string Token { get; set; } = string.Empty;

    /// <summary>
    /// When the access token expires (UTC).
    /// Client should refresh before this time.
    /// </summary>
    public DateTime ExpiresAt { get; set; }

    /// <summary>
    /// Refresh token for obtaining new access tokens (7-day lifetime).
    /// Keep this securely stored (not in JWT).
    /// Use in POST /api/auth/refresh request body.
    /// </summary>
    public string? RefreshToken { get; set; }

    /// <summary>
    /// When the refresh token expires (UTC).
    /// If refresh token is about to expire, user should re-login.
    /// </summary>
    public DateTime? RefreshTokenExpiresAt { get; set; }
}
