namespace ServiceMarketplace.Application.Interfaces;

/// <summary>
/// Service for cleaning up expired and invalid data.
/// Runs as background service to maintain database health.
/// </summary>
public interface ICleanupService
{
    /// <summary>
    /// Deletes expired refresh tokens from the database.
    /// Tokens are considered expired if their ExpiresAt date has passed.
    /// </summary>
    /// <returns>Number of tokens deleted</returns>
    Task<int> DeleteExpiredRefreshTokensAsync();

    /// <summary>
    /// Cleans up abandoned sessions.
    /// Sessions are considered abandoned if they have a Login event but no Logout/SessionExpired
    /// event within a configurable time period (e.g., 24 hours).
    /// </summary>
    /// <returns>Number of sessions cleaned up</returns>
    Task<int> CleanupAbandonedSessionsAsync();

    /// <summary>
    /// Archives old audit logs to separate table or deletes them.
    /// Logs older than the retention period (e.g., 90 days) are moved to archive.
    /// </summary>
    /// <param name="retentionDays">Number of days to retain audit logs</param>
    /// <returns>Number of logs archived or deleted</returns>
    Task<int> ArchiveOldAuditLogsAsync(int retentionDays = 90);
}
