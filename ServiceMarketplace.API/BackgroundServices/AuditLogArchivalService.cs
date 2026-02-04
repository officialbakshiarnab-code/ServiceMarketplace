using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ServiceMarketplace.Application.Interfaces;

namespace ServiceMarketplace.API.BackgroundServices;

/// <summary>
/// Background service that periodically archives old audit logs.
/// Runs once per day to move or delete audit logs older than retention period.
/// Implements safe archival that can be re-run without side effects.
/// </summary>
public sealed class AuditLogArchivalService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<AuditLogArchivalService> _logger;
    private readonly TimeSpan _archivalInterval = TimeSpan.FromDays(1);
    private readonly int _retentionDays;

    public AuditLogArchivalService(
        IServiceProvider serviceProvider,
        ILogger<AuditLogArchivalService> logger,
        int retentionDays = 90)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _retentionDays = retentionDays;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("[AuditLogArchivalService] Service started (retention: {Days} days)", 
            _retentionDays);

        // Wait 1 hour before first run to avoid startup load
        await Task.Delay(TimeSpan.FromHours(1), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                _logger.LogInformation("[AuditLogArchivalService] Starting archival cycle");

                using var scope = _serviceProvider.CreateScope();
                var cleanupService = scope.ServiceProvider.GetRequiredService<ICleanupService>();

                var archivedCount = await cleanupService.ArchiveOldAuditLogsAsync(_retentionDays);

                _logger.LogInformation("[AuditLogArchivalService] Archival cycle completed. Archived {Count} logs",
                    archivedCount);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[AuditLogArchivalService] Error during archival cycle");
            }

            // Wait for next archival interval
            try
            {
                await Task.Delay(_archivalInterval, stoppingToken);
            }
            catch (TaskCanceledException)
            {
                // Expected when stopping the service
                break;
            }
        }

        _logger.LogInformation("[AuditLogArchivalService] Service stopped");
    }
}
