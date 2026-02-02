namespace ServiceMarketplace.Domain.Entities;

/// <summary>
/// Audit log for tracking authentication events.
/// Each event is recorded as a new row (append-only).
/// </summary>
public class AuditLog
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Reference to AspNetUsers.Id
    /// </summary>
    public string UserId { get; set; } = null!;

    /// <summary>
    /// Role associated with the session at time of event.
    /// </summary>
    public string? Role { get; set; }

    /// <summary>
    /// Event type (Login, Logout, SessionExpired).
    /// </summary>
    public string EventType { get; set; } = "Login";

    /// <summary>
    /// UTC timestamp for the event.
    /// </summary>
    public DateTime TimestampUtc { get; set; }

    /// <summary>
    /// Unique session identifier from JWT 'jti' claim.
    /// Nullable for events without session context.
    /// </summary>
    public string? SessionId { get; set; }

    /// <summary>
    /// Client IP address (IPv4 or IPv6).
    /// </summary>
    public string? IpAddress { get; set; }

    /// <summary>
    /// HTTP User-Agent header from client.
    /// </summary>
    public string? UserAgent { get; set; }
}
