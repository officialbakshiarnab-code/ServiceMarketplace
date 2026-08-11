using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ServiceMarketplace.Application.Constants;
using ServiceMarketplace.Application.DTOs;
using ServiceMarketplace.Application.Interfaces;
using ServiceMarketplace.Domain.Enums;
using ServiceMarketplace.Infrastructure.Data;
using ServiceMarketplace.Infrastructure.Data.Extensions;

namespace ServiceMarketplace.Infrastructure.Services;

/// <summary>
/// Service for retrieving admin dashboard KPI metrics.
/// Uses efficient queries to minimize database roundtrips and N+1 problems.
/// </summary>
public sealed class AdminKpiService(
    AppDbContext context,
    ILogger<AdminKpiService> logger) : IAdminKpiService
{
    public async Task<AdminDashboardKpiDto> GetDashboardKpisAsync()
    {
        logger.LogInformation("[AdminKpiService] Fetching dashboard KPIs");

        // Execute all queries in parallel for performance
        var userCountsTask = GetUserCountsAsync();
        var requestStatsTask = GetRequestStatsAsync();
        var bidStatsTask = GetBidStatsAsync();
        var authStatsTask = GetAuthStatsAsync();
        var recentEventsTask = GetRecentAuditEventsAsync();

        await Task.WhenAll(userCountsTask, requestStatsTask, bidStatsTask, authStatsTask, recentEventsTask);

        var kpis = new AdminDashboardKpiDto
        {
            UserCounts = await userCountsTask,
            RequestStats = await requestStatsTask,
            BidStats = await bidStatsTask,
            AuthStats = await authStatsTask,
            RecentAuditEvents = await recentEventsTask
        };

        logger.LogInformation("[AdminKpiService] KPIs fetched successfully");

        return kpis;
    }

    private async Task<UserCountsByRoleDto> GetUserCountsAsync()
    {
        var users = await context.Users
            .AsNoTracking()
            .Include(u => u.UserRoles)
            .ThenInclude(ur => ur.Role)
            .ToListAsync();

        var userCounts = new UserCountsByRoleDto
        {
            TotalUsers = users.Count,
            UsersCount = users.Count(HasServiceCustomerCapability),
            ServiceProvidersCount = users.Count(HasServiceProviderCapability),
            AdminsCount = users.Count(HasPlatformAdminPermission)
        };

        logger.LogInformation("[AdminKpiService] User counts: Total={Total}, Users={Users}, Providers={Providers}, Admins={Admins}",
            userCounts.TotalUsers, userCounts.UsersCount, userCounts.ServiceProvidersCount, userCounts.AdminsCount);

        return userCounts;
    }

    private static bool HasServiceCustomerCapability(Domain.Entities.User user)
    {
        return MarketplaceCapabilityConstants
            .FromRoles(GetRoleNames(user))
            .Contains(MarketplaceCapabilityConstants.ServiceCustomer);
    }

    private static bool HasServiceProviderCapability(Domain.Entities.User user)
    {
        return MarketplaceCapabilityConstants
            .FromRoles(GetRoleNames(user))
            .Contains(MarketplaceCapabilityConstants.ServiceProvider);
    }

    private static bool HasPlatformAdminPermission(Domain.Entities.User user)
    {
        return AdministrativePermissionConstants
            .FromRoles(GetRoleNames(user))
            .Contains(AdministrativePermissionConstants.PlatformAdmin);
    }

    private static IEnumerable<string> GetRoleNames(Domain.Entities.User user)
    {
        var roleNames = user.UserRoles
            .Select(ur => ur.Role.Name)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Select(name => name!)
            .ToList();

        if (roleNames.Count > 0)
            return roleNames;

        return new[] { user.UserType.GetPrimaryRole() };
    }

    private async Task<ServiceRequestStatsDto> GetRequestStatsAsync()
    {
        // Single query with grouping to get all stats at once
        var stats = await context.ServiceRequests
            .GroupBy(r => 1)
            .Select(g => new
            {
                Total = g.Count(),
                Open = g.Count(r => r.Status == ServiceRequestStatus.Open),
                Accepted = g.Count(r => r.Status == ServiceRequestStatus.Accepted),
                Completed = g.Count(r => r.Status == ServiceRequestStatus.Closed)
            })
            .FirstOrDefaultAsync();

        if (stats == null)
        {
            return new ServiceRequestStatsDto
            {
                TotalRequests = 0,
                OpenRequests = 0,
                AcceptedRequests = 0,
                CompletedRequests = 0,
                CompletionRate = 0
            };
        }

        var completionRate = stats.Total > 0
            ? Math.Round((double)stats.Completed / stats.Total * 100, 2)
            : 0;

        logger.LogInformation("[AdminKpiService] Request stats: Total={Total}, Open={Open}, Accepted={Accepted}, Completed={Completed}, CompletionRate={Rate}%",
            stats.Total, stats.Open, stats.Accepted, stats.Completed, completionRate);

        return new ServiceRequestStatsDto
        {
            TotalRequests = stats.Total,
            OpenRequests = stats.Open,
            AcceptedRequests = stats.Accepted,
            CompletedRequests = stats.Completed,
            CompletionRate = completionRate
        };
    }

    private async Task<BidStatsDto> GetBidStatsAsync()
    {
        // Single query with grouping to get all bid stats
        var stats = await context.Bids
            .GroupBy(b => 1)
            .Select(g => new
            {
                Total = g.Count(),
                Pending = g.Count(b => b.Status == BidStatus.Pending),
                Accepted = g.Count(b => b.Status == BidStatus.Accepted),
                Rejected = g.Count(b => b.Status == BidStatus.Rejected)
            })
            .FirstOrDefaultAsync();

        var requestCount = await context.ServiceRequests.CountAsync();

        if (stats == null || requestCount == 0)
        {
            return new BidStatsDto
            {
                TotalBids = 0,
                PendingBids = 0,
                AcceptedBids = 0,
                RejectedBids = 0,
                AverageBidsPerRequest = 0
            };
        }

        var averageBidsPerRequest = requestCount > 0
            ? Math.Round((double)stats.Total / requestCount, 2)
            : 0;

        logger.LogInformation("[AdminKpiService] Bid stats: Total={Total}, Pending={Pending}, Accepted={Accepted}, Rejected={Rejected}, Avg={Avg}",
            stats.Total, stats.Pending, stats.Accepted, stats.Rejected, averageBidsPerRequest);

        return new BidStatsDto
        {
            TotalBids = stats.Total,
            PendingBids = stats.Pending,
            AcceptedBids = stats.Accepted,
            RejectedBids = stats.Rejected,
            AverageBidsPerRequest = averageBidsPerRequest
        };
    }

    private async Task<AuthStatsDto> GetAuthStatsAsync()
    {
        var thirtyDaysAgo = DateTime.UtcNow.AddDays(-30);
        var oneHourAgo = DateTime.UtcNow.AddHours(-1);

        // Get auth events from last 30 days
        var recentAuthEvents = await context.AuditLogs
            .Where(a => a.TimestampUtc >= thirtyDaysAgo)
            .GroupBy(a => a.EventType)
            .Select(g => new
            {
                EventType = g.Key,
                Count = g.Count()
            })
            .ToListAsync();

        var successfulLogins = recentAuthEvents
            .FirstOrDefault(e => e.EventType == "Login")?.Count ?? 0;

        var failedLoginAttempts = recentAuthEvents
            .FirstOrDefault(e => e.EventType == "LoginFailed")?.Count ?? 0;

        var sessionExpirations = recentAuthEvents
            .FirstOrDefault(e => e.EventType == "SessionExpired")?.Count ?? 0;

        // Estimate active sessions: Recent logins without corresponding logout/expiry
        var recentLogins = await context.AuditLogs
            .Where(a => a.EventType == "Login" && a.TimestampUtc >= oneHourAgo)
            .Select(a => a.SessionId)
            .Distinct()
            .ToListAsync();

        var loggedOutSessions = await context.AuditLogs
            .Where(a => (a.EventType == "Logout" || a.EventType == "SessionExpired")
                        && recentLogins.Contains(a.SessionId))
            .Select(a => a.SessionId)
            .Distinct()
            .ToListAsync();

        var activeSessions = recentLogins.Except(loggedOutSessions).Count();

        logger.LogInformation("[AdminKpiService] Auth stats: Logins={Logins}, Failed={Failed}, Expired={Expired}, Active={Active}",
            successfulLogins, failedLoginAttempts, sessionExpirations, activeSessions);

        return new AuthStatsDto
        {
            SuccessfulLogins = successfulLogins,
            FailedLoginAttempts = failedLoginAttempts,
            SessionExpirations = sessionExpirations,
            ActiveSessions = activeSessions
        };
    }

    private async Task<List<RecentAuditEventDto>> GetRecentAuditEventsAsync()
    {
        var recentEvents = await context.AuditLogs
            .OrderByDescending(a => a.TimestampUtc)
            .Take(10)
            .Select(a => new RecentAuditEventDto
            {
                EventType = a.EventType,
                UserId = a.UserId,
                Role = a.Role,
                TimestampUtc = a.TimestampUtc,
                IpAddress = a.IpAddress,
                UserAgent = a.UserAgent
            })
            .ToListAsync();

        logger.LogInformation("[AdminKpiService] Retrieved {Count} recent audit events", recentEvents.Count);

        return recentEvents;
    }
}
