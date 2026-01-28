namespace ServiceMarketplace.Domain.Entities;

/// <summary>
/// Audit log for tracking user login sessions.
/// Records login time, logout time, and metadata for security monitoring.
/// </summary>
public class LoginAuditLog
{
    public Guid Id { get; set; } = Guid.NewGuid();
    
    /// <summary>
    /// Reference to AspNetUsers.Id
    /// </summary>
    public string UserId { get; set; } = null!;
    
    /// <summary>
    /// Login provider type. Default is "JWT" for token-based auth.
    /// </summary>
    public string LoginProvider { get; set; } = "JWT";
    
    /// <summary>
    /// Unique session identifier from JWT 'jti' claim.
    /// Used to correlate login/logout events and enable session revocation.
    /// </summary>
    public string SessionId { get; set; } = null!;
    
    /// <summary>
    /// UTC timestamp when user logged in.
    /// </summary>
    public DateTime LoginTime { get; set; }
    
    /// <summary>
    /// UTC timestamp when user explicitly logged out.
    /// Null if session is still active or expired without logout.
    /// </summary>
    public DateTime? LogoutTime { get; set; }
    
    /// <summary>
    /// Client IP address (IPv4 or IPv6).
    /// May be null if not available or behind proxy.
    /// </summary>
    public string? IpAddress { get; set; }
    
    /// <summary>
    /// HTTP User-Agent header from client.
    /// Helps identify browser/device type.
    /// </summary>
    public string? UserAgent { get; set; }
    
    /// <summary>
    /// Platform identifier (Web, MAUI-Android, MAUI-iOS, MAUI-Windows).
    /// Sent via X-Platform header from client.
    /// </summary>
    public string? Platform { get; set; }
}
