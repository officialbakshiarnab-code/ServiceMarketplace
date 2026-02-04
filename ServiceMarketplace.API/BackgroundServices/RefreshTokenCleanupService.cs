using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ServiceMarketplace.Application.Interfaces;

namespace ServiceMarketplace.API.BackgroundServices;

/// <summary>
/// Background service that periodically deletes expired refresh tokens.
/// Runs every hour to keep the RefreshTokens table clean.
/// Implements idempotent cleanup that's safe to run multiple times.
/// </summary>
public sealed class RefreshTokenCleanupService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<RefreshTokenCleanupService> _logger;
    private readonly TimeSpan _cleanupInterval = TimeSpan.FromHours(1);

    public RefreshTokenCleanupService(
        IServiceProvider serviceProvider,
        ILogger<RefreshTokenCleanupService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("[RefreshTokenCleanupService] Service started");

        // Wait 5 minutes before first run to allow app to fully start
        await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                _logger.LogInformation("[RefreshTokenCleanupService] Starting cleanup cycle");

                using var scope = _serviceProvider.CreateScope();
                var cleanupService = scope.ServiceProvider.GetRequiredService<ICleanupService>();

                var deletedCount = await cleanupService.DeleteExpiredRefreshTokensAsync();

                _logger.LogInformation("[RefreshTokenCleanupService] Cleanup cycle completed. Deleted {Count} tokens",
                    deletedCount);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[RefreshTokenCleanupService] Error during cleanup cycle");
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

        _logger.LogInformation("[RefreshTokenCleanupService] Service stopped");
    }
}
