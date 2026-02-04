namespace ServiceMarketplace.Domain.Entities;

/// <summary>
/// Stores issued refresh tokens for server-side validation and rotation.
/// 
/// LIFECYCLE:
/// 1. Token issued at login: IsActive=true, RevokedAt=null
/// 2. Token used for refresh: New token created, old marked as revoked
/// 3. Token expires naturally: IsActive=false (exp date passed), not used
/// 4. Token explicitly revoked: RevokedAt set (e.g., logout, password change)
/// 
/// SECURITY PROPERTIES:
/// - Server keeps complete history of all refresh tokens
/// - Can revoke all sessions for a user (set RevokedAt on all tokens)
/// - Can detect token reuse attacks (using old token after rotation)
/// - Enables logout on all devices (revoke all tokens for user)
/// </summary>
public class RefreshTokenEntity
{
    /// <summary>
    /// Unique identifier for this refresh token record.
    /// </summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// User ID who owns this token.
    /// Foreign key to Users.Id.
    /// </summary>
    public string UserId { get; set; } = string.Empty;

    /// <summary>
    /// Session ID from JWT (Jti claim).
    /// Links refresh token to its access token.
    /// Enables tracking complete session lifecycle.
    /// </summary>
    public string SessionId { get; set; } = string.Empty;

    /// <summary>
    /// The actual refresh token value (hashed in production).
    /// Base64-encoded random 32-byte value.
    /// SECURITY: Store hashed, never store plaintext.
    /// </summary>
    public string TokenHash { get; set; } = string.Empty;

    /// <summary>
    /// When this refresh token was issued (UTC).
    /// </summary>
    public DateTime IssuedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// When this refresh token expires (UTC).
    /// Typically 7 days from IssuedAt.
    /// After this, token is invalid regardless of active status.
    /// </summary>
    public DateTime ExpiresAt { get; set; }

    /// <summary>
    /// When this refresh token was explicitly revoked (UTC), if at all.
    /// NULL = not revoked (still active or naturally expired)
    /// NOT NULL = explicitly revoked (e.g., logout, password change, token reuse detected)
    /// </summary>
    public DateTime? RevokedAt { get; set; }

    /// <summary>
    /// Reason for revocation (if revoked).
    /// Examples: "Logout", "PasswordChanged", "TokenReuseDetected", "AdminRevoked"
    /// </summary>
    public string? RevocationReason { get; set; }

    /// <summary>
    /// IP address where this token was issued.
    /// Used for anomaly detection (e.g., token used from different country).
    /// </summary>
    public string? IssuedFromIpAddress { get; set; }

    /// <summary>
    /// User-Agent when token was issued.
    /// Helps detect stolen tokens (e.g., used from different browser).
    /// </summary>
    public string? IssuedFromUserAgent { get; set; }

    /// <summary>
    /// IP address where token was USED for refresh.
    /// May differ from IssuedFromIpAddress (e.g., user roaming).
    /// Multiple values indicate roaming, not token theft.
    /// </summary>
    public string? LastUsedIpAddress { get; set; }

    /// <summary>
    /// When token was last used for refresh (UTC).
    /// NULL = never used yet (fresh token from login)
    /// </summary>
    public DateTime? LastUsedAt { get; set; }

    /// <summary>
    /// Family ID for refresh token rotation.
    /// All tokens in same family (rotation chain) share this ID.
    /// Used to detect token reuse (using old token after rotation).
    /// </summary>
    public string TokenFamily { get; set; } = string.Empty;

    /// <summary>
    /// Is this token currently active?
    /// True = not revoked AND not expired
    /// False = revoked OR expired
    /// 
    /// COMPUTED PROPERTY (in code, not database):
    /// Active = RevokedAt == null AND ExpiresAt > UtcNow
    /// </summary>
    public bool IsActive => RevokedAt == null && ExpiresAt > DateTime.UtcNow;
}
