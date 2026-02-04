namespace ServiceMarketplace.Application.DTOs;

/// <summary>
/// Request for querying audit logs with filtering and pagination.
/// Admin-only endpoint for auditing and compliance.
/// </summary>
public class AuditLogQueryRequest
{
    /// <summary>
    /// Filter by specific user ID (optional).
    /// </summary>
    public string? UserId { get; set; }

    /// <summary>
    /// Filter by event type (Login, Logout, SessionExpired, Registration).
    /// </summary>
    public string? EventType { get; set; }

    /// <summary>
    /// Filter by role (User, ServiceProvider, Admin).
    /// </summary>
    public string? Role { get; set; }

    /// <summary>
    /// Start date for date range filter (UTC, inclusive).
    /// </summary>
    public DateTime? StartDate { get; set; }

    /// <summary>
    /// End date for date range filter (UTC, inclusive).
    /// </summary>
    public DateTime? EndDate { get; set; }

    /// <summary>
    /// Page number (1-based).
    /// </summary>
    public int Page { get; set; } = 1;

    /// <summary>
    /// Number of records per page (max 100).
    /// </summary>
    public int PageSize { get; set; } = 20;

    /// <summary>
    /// Sort order: "asc" or "desc" (default: desc - newest first).
    /// </summary>
    public string SortOrder { get; set; } = "desc";
}
