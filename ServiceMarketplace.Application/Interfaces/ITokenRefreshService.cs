using ServiceMarketplace.Application.DTOs;

namespace ServiceMarketplace.Application.Interfaces;

/// <summary>
/// Token refresh service for managing JWT token lifecycle and rotation.
/// 
/// DESIGN PRINCIPLES:
/// 1. Access tokens are short-lived (10 minutes)
/// 2. Refresh tokens are long-lived (7 days, rotated on each use)
/// 3. Refresh is idempotent (same request yields same result)
/// 4. Server-side token storage enables revocation (logout all devices)
/// 5. Token families track rotation chains (detect token reuse attacks)
/// </summary>
public interface ITokenRefreshService
{
    /// <summary>
    /// Issues new refresh and access tokens at login.
    /// Called once per successful authentication.
    /// </summary>
    /// <param name="userId">User ID from Identity.Users</param>
    /// <param name="sessionId">Session ID (JWT jti claim)</param>
    /// <param name="ipAddress">Client IP address</param>
    /// <param name="userAgent">HTTP User-Agent header</param>
    /// <returns>Refresh token to store on client, expires in 7 days</returns>
    Task<string> IssueRefreshTokenAsync(
        string userId,
        string sessionId,
        string? ipAddress,
        string? userAgent);

    /// <summary>
    /// Refreshes an expired access token using a valid refresh token.
    /// 
    /// IDEMPOTENCY GUARANTEE:
    /// - Same refresh token submitted multiple times within 30 seconds
    ///   returns the SAME new tokens (deduplicated by family ID)
    /// - Safe for automatic retries
    /// - Prevents request amplification attacks
    /// 
    /// TOKEN ROTATION:
    /// - Old refresh token is marked revoked (can detect reuse)
    /// - New refresh token issued (updated on client)
    /// - Same SessionId maintained across refresh chain
    /// </summary>
    /// <param name="refreshToken">Client's refresh token</param>
    /// <param name="ipAddress">Client IP address (may differ from issued IP if roaming)</param>
    /// <param name="userAgent">HTTP User-Agent</param>
    /// <returns>New access token (10 min) and new refresh token (7 days, rotated)</returns>
    /// <exception cref="InvalidOperationException">Refresh token invalid, expired, or revoked</exception>
    Task<RefreshTokenResponse> RefreshAccessTokenAsync(
        string refreshToken,
        string? ipAddress,
        string? userAgent);

    /// <summary>
    /// Revokes all active refresh tokens for a user (logout from all devices).
    /// Called during password change, account lockout, or admin action.
    /// </summary>
    /// <param name="userId">User ID to revoke tokens for</param>
    /// <param name="reason">Reason for revocation (e.g., "PasswordChanged")</param>
    Task RevokeAllTokensForUserAsync(string userId, string reason);

    /// <summary>
    /// Revokes a specific refresh token (logout from one device).
    /// Called during explicit logout on a device.
    /// </summary>
    /// <param name="refreshToken">Token to revoke</param>
    /// <param name="reason">Reason for revocation (e.g., "UserLogout")</param>
    Task RevokeTokenAsync(string refreshToken, string reason);

    /// <summary>
    /// Detects token reuse attacks.
    /// If an old (revoked) token from a family is used again,
    /// it indicates the token was stolen (someone using old, leaked token).
    /// In this case, revoke the entire token family.
    /// </summary>
    /// <param name="refreshToken">Token to check</param>
    /// <returns>True if token reuse detected (potential theft), false otherwise</returns>
    Task<bool> DetectTokenReuseAsync(string refreshToken);

    /// <summary>
    /// Cleans up expired refresh tokens from database (maintenance task).
    /// Run periodically (daily) to keep database lean.
    /// </summary>
    /// <returns>Number of tokens deleted</returns>
    Task<int> CleanupExpiredTokensAsync();
}
