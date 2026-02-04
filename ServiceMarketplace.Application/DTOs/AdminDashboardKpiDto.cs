namespace ServiceMarketplace.Application.DTOs;

/// <summary>
/// Admin dashboard KPI (Key Performance Indicators) metrics.
/// Provides system-wide statistics for monitoring and analytics.
/// </summary>
public class AdminDashboardKpiDto
{
    /// <summary>
    /// User counts by role.
    /// </summary>
    public UserCountsByRoleDto UserCounts { get; set; } = new();

    /// <summary>
    /// Service request statistics.
    /// </summary>
    public ServiceRequestStatsDto RequestStats { get; set; } = new();

    /// <summary>
    /// Bid statistics.
    /// </summary>
    public BidStatsDto BidStats { get; set; } = new();

    /// <summary>
    /// Authentication statistics.
    /// </summary>
    public AuthStatsDto AuthStats { get; set; } = new();

    /// <summary>
    /// Recent audit events summary.
    /// </summary>
    public List<RecentAuditEventDto> RecentAuditEvents { get; set; } = new();
}

/// <summary>
/// User counts by role.
/// </summary>
public class UserCountsByRoleDto
{
    /// <summary>
    /// Total number of users across all roles.
    /// </summary>
    public int TotalUsers { get; set; }

    /// <summary>
    /// Number of users with User role (can create service requests).
    /// </summary>
    public int UsersCount { get; set; }

    /// <summary>
    /// Number of users with ServiceProvider role (can place bids).
    /// </summary>
    public int ServiceProvidersCount { get; set; }

    /// <summary>
    /// Number of users with Admin role.
    /// </summary>
    public int AdminsCount { get; set; }
}

/// <summary>
/// Service request statistics.
/// </summary>
public class ServiceRequestStatsDto
{
    /// <summary>
    /// Total number of service requests (all statuses).
    /// </summary>
    public int TotalRequests { get; set; }

    /// <summary>
    /// Number of open requests (accepting bids).
    /// </summary>
    public int OpenRequests { get; set; }

    /// <summary>
    /// Number of accepted requests (bid accepted, in progress).
    /// </summary>
    public int AcceptedRequests { get; set; }

    /// <summary>
    /// Number of closed/completed requests.
    /// </summary>
    public int CompletedRequests { get; set; }

    /// <summary>
    /// Percentage of requests that have been completed.
    /// </summary>
    public double CompletionRate { get; set; }
}

/// <summary>
/// Bid statistics.
/// </summary>
public class BidStatsDto
{
    /// <summary>
    /// Total number of bids placed (all statuses).
    /// </summary>
    public int TotalBids { get; set; }

    /// <summary>
    /// Number of pending bids (not yet accepted or rejected).
    /// </summary>
    public int PendingBids { get; set; }

    /// <summary>
    /// Number of accepted bids.
    /// </summary>
    public int AcceptedBids { get; set; }

    /// <summary>
    /// Number of rejected bids.
    /// </summary>
    public int RejectedBids { get; set; }

    /// <summary>
    /// Average number of bids per request.
    /// </summary>
    public double AverageBidsPerRequest { get; set; }
}

/// <summary>
/// Authentication statistics.
/// </summary>
public class AuthStatsDto
{
    /// <summary>
    /// Total number of successful logins (last 30 days).
    /// </summary>
    public int SuccessfulLogins { get; set; }

    /// <summary>
    /// Total number of failed login attempts (last 30 days).
    /// Calculated from LoginFailed audit events.
    /// </summary>
    public int FailedLoginAttempts { get; set; }

    /// <summary>
    /// Total number of session expirations (last 30 days).
    /// </summary>
    public int SessionExpirations { get; set; }

    /// <summary>
    /// Number of currently active sessions.
    /// Estimated from recent logins without corresponding logout/expiry.
    /// </summary>
    public int ActiveSessions { get; set; }
}

/// <summary>
/// Recent audit event summary.
/// </summary>
public class RecentAuditEventDto
{
    /// <summary>
    /// Event type (Login, Logout, Registration, SessionExpired, etc.).
    /// </summary>
    public string EventType { get; set; } = string.Empty;

    /// <summary>
    /// User ID who triggered the event.
    /// </summary>
    public string UserId { get; set; } = string.Empty;

    /// <summary>
    /// User role at time of event.
    /// </summary>
    public string? Role { get; set; }

    /// <summary>
    /// When the event occurred (UTC).
    /// </summary>
    public DateTime TimestampUtc { get; set; }

    /// <summary>
    /// IP address of the client.
    /// </summary>
    public string? IpAddress { get; set; }

    /// <summary>
    /// User agent string.
    /// </summary>
    public string? UserAgent { get; set; }
}
