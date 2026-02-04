namespace ServiceMarketplace.Application.DTOs;

/// <summary>
/// Authentication result payload with access and refresh tokens.
///
/// TOKEN LIFECYCLE:
/// - Access Token: 10-minute lifetime, includes all claims, passed in Authorization header
/// - Refresh Token: 7-day lifetime, stored securely, used to refresh access token
///
/// CLIENT BEHAVIOR:
/// 1. On login: Store both tokens
/// 2. Use access token for API calls
/// 3. Before access token expires: Use refresh token to get new access token
/// 4. Store new tokens (refresh token is rotated)
/// 5. On logout: Discard both tokens
/// </summary>
public sealed class AuthResultDto
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
