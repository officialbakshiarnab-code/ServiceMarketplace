namespace ServiceMarketplace.Domain.Entities;

/// <summary>
/// Audit log for tracking authentication events.
/// Each event is recorded as a new row (append-only).
/// </summary>
public class AuditLog
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Reference to Users.Id
    /// </summary>
    public string UserId { get; set; } = null!;

    public string? Role { get; set; }
    public string EventType { get; set; } = "Login";
    public DateTime TimestampUtc { get; set; }
    public string? SessionId { get; set; }
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
}
