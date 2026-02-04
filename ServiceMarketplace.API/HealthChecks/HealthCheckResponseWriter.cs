using Microsoft.Extensions.Diagnostics.HealthChecks;
using System.Text.Json;

namespace ServiceMarketplace.API.HealthChecks;

/// <summary>
/// Custom health check response writer that produces UI-friendly JSON output.
/// Includes detailed information about each health check.
/// </summary>
public static class HealthCheckResponseWriter
{
    public static Task WriteResponse(HttpContext context, HealthReport report)
    {
        context.Response.ContentType = "application/json";

        var response = new
        {
            status = report.Status.ToString(),
            timestamp = DateTime.UtcNow,
            totalDuration = report.TotalDuration.TotalMilliseconds,
            checks = report.Entries.Select(entry => new
            {
                name = entry.Key,
                status = entry.Value.Status.ToString(),
                description = entry.Value.Description ?? "No description",
                duration = entry.Value.Duration.TotalMilliseconds,
                exception = entry.Value.Exception?.Message,
                data = entry.Value.Data
            }),
            summary = new
            {
                total = report.Entries.Count,
                healthy = report.Entries.Count(e => e.Value.Status == HealthStatus.Healthy),
                degraded = report.Entries.Count(e => e.Value.Status == HealthStatus.Degraded),
                unhealthy = report.Entries.Count(e => e.Value.Status == HealthStatus.Unhealthy)
            }
        };

        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        return context.Response.WriteAsync(
            JsonSerializer.Serialize(response, options));
    }
}
