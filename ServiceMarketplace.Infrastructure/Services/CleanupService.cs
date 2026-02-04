using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ServiceMarketplace.Application.Interfaces;
using ServiceMarketplace.Infrastructure.Data;

namespace ServiceMarketplace.Infrastructure.Services;

/// <summary>
/// Service for cleaning up expired and invalid data.
/// Implements idempotent cleanup operations that can be safely retried.
/// </summary>
public sealed class CleanupService(
    AppDbContext context,
    ILogger<CleanupService> logger) : ICleanupService
{
    public async Task<int> DeleteExpiredRefreshTokensAsync()
    {
        try
        {
            logger.LogInformation("[CleanupService] Starting expired refresh token cleanup");

            var now = DateTime.UtcNow;

            // Find expired tokens
            var expiredTokens = await context.RefreshTokens
                .Where(rt => rt.ExpiresAt < now || rt.RevokedAt != null)
                .ToListAsync();

            if (expiredTokens.Count == 0)
            {
                logger.LogInformation("[CleanupService] No expired refresh tokens found");
                return 0;
            }

            // Delete expired tokens
            context.RefreshTokens.RemoveRange(expiredTokens);
            await context.SaveChangesAsync();

            logger.LogInformation("[CleanupService] Deleted {Count} expired refresh tokens", expiredTokens.Count);

            return expiredTokens.Count;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[CleanupService] Error deleting expired refresh tokens");
            return 0;
        }
    }

    public async Task<int> CleanupAbandonedSessionsAsync()
    {
        try
        {
            logger.LogInformation("[CleanupService] Starting abandoned session cleanup");

            var abandonedThreshold = DateTime.UtcNow.AddHours(-24);

            // Find sessions with Login but no Logout/SessionExpired within 24 hours
            var abandonedSessions = await context.AuditLogs
                .Where(a => a.EventType == "Login" && a.TimestampUtc < abandonedThreshold)
                .Select(a => a.SessionId)
                .Distinct()
                .ToListAsync();

            if (abandonedSessions.Count == 0)
            {
                logger.LogInformation("[CleanupService] No abandoned sessions found");
                return 0;
            }

            // Check which sessions have logout/expiry events
            var completedSessions = await context.AuditLogs
                .Where(a => (a.EventType == "Logout" || a.EventType == "SessionExpired")
                            && abandonedSessions.Contains(a.SessionId))
                .Select(a => a.SessionId)
                .Distinct()
                .ToListAsync();

            // Sessions without logout/expiry are abandoned
            var trulyAbandonedSessions = abandonedSessions
                .Except(completedSessions)
                .ToList();

            if (trulyAbandonedSessions.Count == 0)
            {
                logger.LogInformation("[CleanupService] No truly abandoned sessions found");
                return 0;
            }

            // Create SessionExpired events for abandoned sessions
            var sessionExpiredEvents = trulyAbandonedSessions
                .Select(sessionId =>
                {
                    // Get the original login event to populate user info
                    var loginEvent = context.AuditLogs
                        .FirstOrDefault(a => a.SessionId == sessionId && a.EventType == "Login");

                    return new Domain.Entities.AuditLog
                    {
                        UserId = loginEvent?.UserId ?? "unknown",
                        Role = loginEvent?.Role,
                        EventType = "SessionExpired",
                        TimestampUtc = DateTime.UtcNow,
                        SessionId = sessionId,
                        IpAddress = loginEvent?.IpAddress,
                        UserAgent = "BackgroundCleanupService"
                    };
                })
                .ToList();

            context.AuditLogs.AddRange(sessionExpiredEvents);
            await context.SaveChangesAsync();

            logger.LogInformation("[CleanupService] Cleaned up {Count} abandoned sessions", 
                trulyAbandonedSessions.Count);

            return trulyAbandonedSessions.Count;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[CleanupService] Error cleaning up abandoned sessions");
            return 0;
        }
    }

    public async Task<int> ArchiveOldAuditLogsAsync(int retentionDays = 90)
    {
        try
        {
            logger.LogInformation("[CleanupService] Starting old audit log archival (retention: {Days} days)", 
                retentionDays);

            var archiveThreshold = DateTime.UtcNow.AddDays(-retentionDays);

            // Count old audit logs
            var oldLogsCount = await context.AuditLogs
                .Where(a => a.TimestampUtc < archiveThreshold)
                .CountAsync();

            if (oldLogsCount == 0)
            {
                logger.LogInformation("[CleanupService] No old audit logs to archive");
                return 0;
            }

            // In a real implementation, you might:
            // 1. Copy logs to an archive table
            // 2. Export to blob storage
            // 3. Move to data warehouse
            // For now, we'll just delete them (assuming they're backed up elsewhere)

            logger.LogWarning("[CleanupService] Archival not implemented - would archive {Count} logs older than {Date}",
                oldLogsCount, archiveThreshold.ToString("yyyy-MM-dd"));

            // Uncomment to enable deletion:
            // var oldLogs = await context.AuditLogs
            //     .Where(a => a.TimestampUtc < archiveThreshold)
            //     .ToListAsync();
            // context.AuditLogs.RemoveRange(oldLogs);
            // await context.SaveChangesAsync();

            return 0; // Return 0 until archival is implemented
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[CleanupService] Error archiving old audit logs");
            return 0;
        }
    }
}
