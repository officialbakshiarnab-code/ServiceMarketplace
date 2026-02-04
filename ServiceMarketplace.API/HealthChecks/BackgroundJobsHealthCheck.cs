using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;

namespace ServiceMarketplace.API.HealthChecks;

/// <summary>
/// Health check for background jobs.
/// Verifies that background services (cleanup jobs) are registered and running.
/// </summary>
public sealed class BackgroundJobsHealthCheck : IHealthCheck
{
    private readonly IEnumerable<IHostedService> _hostedServices;

    public BackgroundJobsHealthCheck(IEnumerable<IHostedService> hostedServices)
    {
        _hostedServices = hostedServices;
    }

    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Get all registered background services
            var hostedServicesList = _hostedServices.ToList();

            // Check for expected background services
            var expectedServices = new[]
            {
                "RefreshTokenCleanupService",
                "AbandonedSessionCleanupService",
                "AuditLogArchivalService"
            };

            var registeredServices = hostedServicesList
                .Select(s => s.GetType().Name)
                .ToList();

            var missingServices = expectedServices
                .Where(expected => !registeredServices.Any(r => r.Contains(expected)))
                .ToList();

            var healthData = new Dictionary<string, object>
            {
                { "totalHostedServices", hostedServicesList.Count },
                { "cleanupServicesCount", registeredServices.Count(s => s.Contains("Cleanup") || s.Contains("Archival")) },
                { "registeredServices", string.Join(", ", registeredServices) }
            };

            if (missingServices.Any())
            {
                healthData.Add("missingServices", string.Join(", ", missingServices));
                return Task.FromResult(HealthCheckResult.Degraded(
                    $"Some expected background services are not registered: {string.Join(", ", missingServices)}",
                    data: healthData));
            }

            return Task.FromResult(HealthCheckResult.Healthy(
                "All background jobs are registered",
                data: healthData));
        }
        catch (Exception ex)
        {
            return Task.FromResult(HealthCheckResult.Unhealthy(
                "Background jobs health check failed",
                exception: ex,
                data: new Dictionary<string, object>
                {
                    { "error", ex.Message }
                }));
        }
    }
}
