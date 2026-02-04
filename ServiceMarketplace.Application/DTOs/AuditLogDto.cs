namespace ServiceMarketplace.Application.DTOs;

/// <summary>
/// Represents a single audit log entry.
/// </summary>
public class AuditLogDto
{
    public Guid Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string? Role { get; set; }
    public string EventType { get; set; } = string.Empty;
    public DateTime TimestampUtc { get; set; }
    public string? SessionId { get; set; }
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
}
