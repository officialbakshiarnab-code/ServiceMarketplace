using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using ServiceMarketplace.Infrastructure.Data;

namespace ServiceMarketplace.API.HealthChecks;

/// <summary>
/// Health check for database connectivity.
/// Verifies that the application can connect to and query the database.
/// </summary>
public sealed class DatabaseHealthCheck : IHealthCheck
{
    private readonly AppDbContext _context;

    public DatabaseHealthCheck(AppDbContext context)
    {
        _context = context;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Try to query the database with a simple count operation
            var canConnect = await _context.Database.CanConnectAsync(cancellationToken);

            if (!canConnect)
            {
                return HealthCheckResult.Unhealthy(
                    "Cannot connect to database",
                    data: new Dictionary<string, object>
                    {
                        { "connectionString", _context.Database.GetConnectionString()?.Split(';')[0] ?? "unknown" }
                    });
            }

            // Test a simple query to ensure database is responsive
            var userCount = await _context.Users.CountAsync(cancellationToken);

            return HealthCheckResult.Healthy(
                "Database is accessible and responsive",
                data: new Dictionary<string, object>
                {
                    { "userCount", userCount },
                    { "connectionString", _context.Database.GetConnectionString()?.Split(';')[0] ?? "unknown" }
                });
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy(
                "Database health check failed",
                exception: ex,
                data: new Dictionary<string, object>
                {
                    { "error", ex.Message }
                });
        }
    }
}
