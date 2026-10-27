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
    public async Task<PunchRetentionRunResult> ExecuteCycleAsync(CancellationToken stoppingToken = default)
    {
        logger.LogInformation("PunchRetentionWorker starting retention cycle.");
        using var scope = scopeFactory.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<PunchRetentionService>();
        var result = await service.RunRetentionAsync(stoppingToken);
        logger.LogInformation("PunchRetentionWorker cycle completed: {Message}", result.Message);
        return result;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var intervalHours = configuration.GetValue<int?>("PunchRetention:RunIntervalHours") ?? 24;
        intervalHours = Math.Max(1, intervalHours);
        var startupDelaySeconds = configuration.GetValue<int?>("PunchRetention:StartupDelaySeconds") ?? 60;

        logger.LogInformation("PunchRetentionWorker initialized with interval of {IntervalHours} hours.", intervalHours);

        using var timer = new PeriodicTimer(TimeSpan.FromHours(intervalHours));

        try
        {
            if (startupDelaySeconds > 0)
            {
                await Task.Delay(TimeSpan.FromSeconds(startupDelaySeconds), stoppingToken);
            }

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await ExecuteCycleAsync(stoppingToken);
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
