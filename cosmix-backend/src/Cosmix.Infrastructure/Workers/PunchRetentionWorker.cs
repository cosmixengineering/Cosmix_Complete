using Cosmix.Application.Attendance.PunchRetention;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Cosmix.Infrastructure.Workers;

public sealed class PunchRetentionWorker(
    IServiceScopeFactory scopeFactory,
    IConfiguration configuration,
    ILogger<PunchRetentionWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var intervalHours = configuration.GetValue<int?>("PunchRetention:RunIntervalHours") ?? 24;
        intervalHours = Math.Max(1, intervalHours);

        logger.LogInformation("PunchRetentionWorker initialized with interval of {IntervalHours} hours.", intervalHours);

        using var timer = new PeriodicTimer(TimeSpan.FromHours(intervalHours));

        try
        {
            // Initial run after startup delay of 1 minute to allow system boot
            await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    logger.LogInformation("PunchRetentionWorker starting retention cycle.");
                    using var scope = scopeFactory.CreateScope();
                    var service = scope.ServiceProvider.GetRequiredService<PunchRetentionService>();
                    var result = await service.RunRetentionAsync(stoppingToken);
                    logger.LogInformation("PunchRetentionWorker cycle completed: {Message}", result.Message);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Error occurred during GPS punch retention execution.");
                }

                await timer.WaitForNextTickAsync(stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Graceful shutdown
        }
    }
}
