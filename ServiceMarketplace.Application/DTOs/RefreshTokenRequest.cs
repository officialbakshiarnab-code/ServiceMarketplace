namespace ServiceMarketplace.Application.DTOs;

/// <summary>
/// Request to refresh an expired or expiring access token.
/// The refresh token is sent in the request body (for stateless operation).
/// 
/// IDEMPOTENCY GUARANTEE:
/// - Same RefreshToken sent multiple times produces same result
/// - No side effects from duplicate requests
/// - Safe for network retries
/// </summary>
public class RefreshTokenRequest
{
    /// <summary>
    /// The refresh token (long-lived, typically 7+ days).
    /// Used to obtain a new access token without re-authenticating.
    /// </summary>
    public string RefreshToken { get; set; } = string.Empty;
}
