using ServiceMarketplace.Application.DTOs;

namespace ServiceMarketplace.Application.Interfaces;

/// <summary>
/// Application contract for centralized audit logging.
/// All authentication and authorization events must be logged through this service.
/// </summary>
public interface IAuditLogService
{
    /// <summary>
    /// Records a Registration event in the audit log.
    /// </summary>
    /// <param name="userId">User identifier from ASP.NET Identity</param>
    /// <param name="role">User's assigned role</param>
    Task LogRegistrationAsync(string userId, string role);

    /// <summary>
    /// Records a Login event in the audit log.
    /// </summary>
    /// <param name="userId">User identifier from ASP.NET Identity</param>
    /// <param name="role">User's role at time of login</param>
    /// <param name="sessionId">Unique session identifier (from JWT jti claim)</param>
    /// <param name="ipAddress">Client IP address</param>
    /// <param name="userAgent">HTTP User-Agent header</param>
    Task LogLoginAsync(string userId, string? role, string sessionId, string? ipAddress, string? userAgent);

    /// <summary>
    /// Records a Logout event in the audit log.
    /// </summary>
    /// <param name="userId">User identifier from ASP.NET Identity</param>
    /// <param name="role">User's role at time of logout</param>
    /// <param name="sessionId">Session identifier from JWT</param>
    /// <param name="ipAddress">Client IP address</param>
    /// <param name="userAgent">HTTP User-Agent header</param>
    Task LogLogoutAsync(string userId, string? role, string sessionId, string? ipAddress, string? userAgent);

    /// <summary>
    /// Records a SessionExpired event in the audit log.
    /// Prevents duplicate entries for the same session.
    /// </summary>
    /// <param name="userId">User identifier from ASP.NET Identity</param>
    /// <param name="role">User's role at time of expiration</param>
    /// <param name="sessionId">Session identifier from JWT</param>
    /// <param name="ipAddress">Client IP address</param>
    /// <param name="userAgent">HTTP User-Agent header</param>
    /// <returns>True if event was logged, false if duplicate was detected</returns>
    Task<bool> LogSessionExpiredAsync(string userId, string? role, string? sessionId, string? ipAddress, string? userAgent);

    /// <summary>
    /// Queries audit logs with filtering and pagination (Admin-only).
    /// </summary>
    /// <param name="request">Query parameters with filters</param>
    /// <returns>Paginated audit log results</returns>
    Task<AuditLogQueryResponse> QueryAuditLogsAsync(AuditLogQueryRequest request);
}
