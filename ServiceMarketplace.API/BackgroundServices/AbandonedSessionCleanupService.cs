using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ServiceMarketplace.Application.Interfaces;

namespace ServiceMarketplace.API.BackgroundServices;

/// <summary>
/// Background service that periodically cleans up abandoned sessions.
/// Runs every 6 hours to mark sessions as expired if they have no logout event.
/// Implements idempotent cleanup that prevents duplicate SessionExpired events.
/// </summary>
public sealed class AbandonedSessionCleanupService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<AbandonedSessionCleanupService> _logger;
    private readonly TimeSpan _cleanupInterval = TimeSpan.FromHours(6);

    public AbandonedSessionCleanupService(
        IServiceProvider serviceProvider,
        ILogger<AbandonedSessionCleanupService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("[AbandonedSessionCleanupService] Service started");

        // Wait 10 minutes before first run
        await Task.Delay(TimeSpan.FromMinutes(10), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                _logger.LogInformation("[AbandonedSessionCleanupService] Starting cleanup cycle");

                using var scope = _serviceProvider.CreateScope();
                var cleanupService = scope.ServiceProvider.GetRequiredService<ICleanupService>();

                var cleanedCount = await cleanupService.CleanupAbandonedSessionsAsync();

                _logger.LogInformation("[AbandonedSessionCleanupService] Cleanup cycle completed. Cleaned {Count} sessions",
                    cleanedCount);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[AbandonedSessionCleanupService] Error during cleanup cycle");
            }

            // Wait for next cleanup interval
            try
            {
                await Task.Delay(_cleanupInterval, stoppingToken);
            }
            catch (TaskCanceledException)
            {
                // Expected when stopping the service
                break;
            }
        }

        _logger.LogInformation("[AbandonedSessionCleanupService] Service stopped");
    }
}
