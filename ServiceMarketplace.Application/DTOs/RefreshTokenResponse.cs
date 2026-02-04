namespace ServiceMarketplace.Application.DTOs;

/// <summary>
/// Response from successful token refresh.
/// Contains new access token (short-lived) and optional refresh token (rotated).
/// 
/// TOKEN ROTATION:
/// - Old refresh token is immediately invalidated
/// - New refresh token issued with every refresh (security best practice)
/// - Client must update stored tokens
/// </summary>
public class RefreshTokenResponse
{
    /// <summary>
    /// New access token (10-minute lifetime).
    /// Short-lived token for API access.
    /// </summary>
    public string AccessToken { get; set; } = string.Empty;

    /// <summary>
    /// When the access token expires (UTC).
    /// Client can schedule automatic refresh before this time.
    /// </summary>
    public DateTime AccessTokenExpiresAt { get; set; }

    /// <summary>
    /// New refresh token (7-day lifetime, rotated).
    /// Replaces the old refresh token (which is now invalid).
    /// Client must store this for next refresh.
    /// </summary>
    public string RefreshToken { get; set; } = string.Empty;

    /// <summary>
    /// When the new refresh token expires (UTC).
    /// Client may want to proactively re-login when this approaches.
    /// </summary>
    public DateTime RefreshTokenExpiresAt { get; set; }
}
