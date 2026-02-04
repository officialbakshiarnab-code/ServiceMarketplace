using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace ServiceMarketplace.API.HealthChecks;

/// <summary>
/// Health check for authentication subsystem.
/// Verifies that ASP.NET Identity and role management are functioning.
/// </summary>
public sealed class AuthSubsystemHealthCheck : IHealthCheck
{
    private readonly UserManager<IdentityUser> _userManager;
    private readonly RoleManager<IdentityRole> _roleManager;

    public AuthSubsystemHealthCheck(
        UserManager<IdentityUser> userManager,
        RoleManager<IdentityRole> roleManager)
    {
        _userManager = userManager;
        _roleManager = roleManager;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Check if roles exist
            var rolesExist = await _roleManager.Roles.AnyAsync(cancellationToken);
            if (!rolesExist)
            {
                return HealthCheckResult.Degraded(
                    "No roles configured in the system",
                    data: new Dictionary<string, object>
                    {
                        { "rolesCount", 0 }
                    });
            }

            // Count roles
            var roleCount = await _roleManager.Roles.CountAsync(cancellationToken);

            // Check if users exist
            var usersExist = await _userManager.Users.AnyAsync(cancellationToken);
            var userCount = await _userManager.Users.CountAsync(cancellationToken);

            // Check if UserManager and RoleManager are functioning
            var healthData = new Dictionary<string, object>
            {
                { "rolesCount", roleCount },
                { "usersCount", userCount },
                { "identityVersion", typeof(IdentityUser).Assembly.GetName().Version?.ToString() ?? "unknown" }
            };

            if (roleCount >= 2 && usersExist) // Expected: User, ServiceProvider, optionally Admin
            {
                return HealthCheckResult.Healthy(
                    "Authentication subsystem is healthy",
                    data: healthData);
            }

            return HealthCheckResult.Degraded(
                "Authentication subsystem is functioning but may not be fully configured",
                data: healthData);
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy(
                "Authentication subsystem health check failed",
                exception: ex,
                data: new Dictionary<string, object>
                {
                    { "error", ex.Message }
                });
        }
    }
}
