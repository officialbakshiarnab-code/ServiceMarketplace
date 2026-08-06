using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using ServiceMarketplace.Infrastructure.Data;

namespace ServiceMarketplace.API.HealthChecks;

/// <summary>
/// Health check for the custom authentication subsystem.
/// </summary>
public sealed class AuthSubsystemHealthCheck(AppDbContext dbContext) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var userCount = await dbContext.Users.CountAsync(cancellationToken);
            var roleCount = await dbContext.Roles.CountAsync(cancellationToken);

            var healthData = new Dictionary<string, object>
            {
                { "usersCount", userCount },
                { "rolesCount", roleCount },
                { "authProvider", "CustomJwt" }
            };

            return roleCount > 0
                ? HealthCheckResult.Healthy("Authentication subsystem is healthy", data: healthData)
                : HealthCheckResult.Degraded("Authentication subsystem is functioning but roles are not seeded", data: healthData);
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy(
                "Authentication subsystem health check failed",
                exception: ex,
                data: new Dictionary<string, object> { { "error", ex.Message } });
        }
    }
}
