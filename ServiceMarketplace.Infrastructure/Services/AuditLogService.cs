using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ServiceMarketplace.Application.DTOs;
using ServiceMarketplace.Application.Interfaces;
using ServiceMarketplace.Domain.Entities;
using ServiceMarketplace.Infrastructure.Data;

namespace ServiceMarketplace.Infrastructure.Services;

/// <summary>
/// Centralized service for audit logging.
/// All authentication and authorization audit events must flow through this service.
/// Ensures consistency, testability, and single responsibility.
/// </summary>
public sealed class AuditLogService(AppDbContext context, ILogger<AuditLogService> logger) : IAuditLogService
{
    public async Task LogRegistrationAsync(string userId, string role)
    {
        await RecordEventAsync(userId, role, "Registration", null, null, null);
        logger.LogInformation("Registration event recorded for user {UserId}, role {Role}", userId, role);
    }

    public async Task LogLoginAsync(string userId, string? role, string sessionId, string? ipAddress, string? userAgent)
    {
        await RecordEventAsync(userId, role, "Login", sessionId, ipAddress, userAgent);
        logger.LogInformation("Login event recorded for user {UserId}, session {SessionId}", userId, sessionId);
    }

    public async Task LogLogoutAsync(string userId, string? role, string sessionId, string? ipAddress, string? userAgent)
    {
        // IMPORTANT: This creates a NEW audit log row with EventType="Logout"
        // This is append-only - we NEVER update existing rows
        // Each logout creates exactly one new AuditLogs record
        await RecordEventAsync(userId, role, "Logout", sessionId, ipAddress, userAgent);
        logger.LogInformation("Logout event recorded for user {UserId}, session {SessionId}", userId, sessionId);
    }

    public async Task<bool> LogSessionExpiredAsync(string userId, string? role, string? sessionId, string? ipAddress, string? userAgent)
    {
        // Prevent duplicate SessionExpired logs for the same session.
        // If SessionId exists and a SessionExpired event is already recorded, skip.
        // This handles edge cases where the token expiry timer fires multiple times
        // or if both client and server detect expiry simultaneously.
        if (!string.IsNullOrWhiteSpace(sessionId))
        {
            var existingExpiry = await context.AuditLogs
                .AsNoTracking()
                .FirstOrDefaultAsync(a => 
                    a.SessionId == sessionId &&
                    a.EventType == "SessionExpired"
                );

            if (existingExpiry != null)
            {
                logger.LogInformation("SessionExpired event already recorded for session {SessionId}", sessionId);
                return false;
            }
        }

        await RecordEventAsync(userId, role, "SessionExpired", sessionId, ipAddress, userAgent);
        logger.LogInformation("SessionExpired event recorded for user {UserId}, session {SessionId}", userId, sessionId);
        return true;
    }

    public async Task<AuditLogQueryResponse> QueryAuditLogsAsync(AuditLogQueryRequest request)
    {
        // Start with base query
        var query = context.AuditLogs.AsNoTracking();

        // Apply filters
        if (!string.IsNullOrWhiteSpace(request.UserId))
        {
            query = query.Where(a => a.UserId == request.UserId);
        }

        if (!string.IsNullOrWhiteSpace(request.EventType))
        {
            query = query.Where(a => a.EventType == request.EventType);
        }

        if (!string.IsNullOrWhiteSpace(request.Role))
        {
            query = query.Where(a => a.Role == request.Role);
        }

        if (request.StartDate.HasValue)
        {
            query = query.Where(a => a.TimestampUtc >= request.StartDate.Value);
        }

        if (request.EndDate.HasValue)
        {
            // Include entire end date (until 23:59:59)
            var endDateInclusive = request.EndDate.Value.Date.AddDays(1).AddTicks(-1);
            query = query.Where(a => a.TimestampUtc <= endDateInclusive);
        }

        // Get total count before pagination
        var totalCount = await query.CountAsync();

        // Apply sorting
        query = request.SortOrder?.ToLower() == "asc"
            ? query.OrderBy(a => a.TimestampUtc)
            : query.OrderByDescending(a => a.TimestampUtc);

        // Validate and apply pagination
        var page = Math.Max(1, request.Page);
        var pageSize = Math.Clamp(request.PageSize, 1, 100);
        var skip = (page - 1) * pageSize;

        var items = await query
            .Skip(skip)
            .Take(pageSize)
            .Select(a => new AuditLogDto
            {
                Id = a.Id,
                UserId = a.UserId,
                Role = a.Role,
                EventType = a.EventType,
                TimestampUtc = a.TimestampUtc,
                SessionId = a.SessionId,
                IpAddress = a.IpAddress,
                UserAgent = a.UserAgent
            })
            .ToListAsync();

        logger.LogInformation("Queried audit logs: TotalCount={TotalCount}, Page={Page}, PageSize={PageSize}",
            totalCount, page, pageSize);

        return new AuditLogQueryResponse
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount
        };
    }

    /// <summary>
    /// Internal method to create and persist an audit log entry.
    /// RULES:
    /// 1. NEVER update existing rows
    /// 2. ALWAYS create a new AuditLogs entity
    /// 3. Use DbContext.Add() - never Update()
    /// 4. Call SaveChangesAsync() to persist
    /// </summary>
    private async Task RecordEventAsync(string userId, string? role, string eventType, string? sessionId, string? ipAddress, string? userAgent)
    {
        // Create NEW audit log entity (append-only)
        var auditLog = new AuditLog
        {
            UserId = userId,
            Role = role,
            EventType = eventType,
            TimestampUtc = DateTime.UtcNow,
            SessionId = sessionId,
            IpAddress = ipAddress,
            UserAgent = userAgent
        };

        // Add to context (not Update - this is a NEW entity)
        context.AuditLogs.Add(auditLog);
        
        // Persist to database
        await context.SaveChangesAsync();
        
        // Temporary debug log to verify insertion
        Console.WriteLine($"AUDIT INSERTED: {eventType} | UserId: {userId} | SessionId: {sessionId} | Timestamp: {auditLog.TimestampUtc:yyyy-MM-dd HH:mm:ss} UTC");
    }
}
