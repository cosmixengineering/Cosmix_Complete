using Cosmix.Application.Attendance.PunchRetention;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Cosmix.Infrastructure.Workers;

public sealed class PunchRetentionWorker(
    IServiceScopeFactory scopeFactory,
    IConfiguration configuration,
    ILogger<PunchRetentionWorker> logger,
    Cosmix.Application.Common.BackgroundJobs.WorkerHealth? health = null) : BackgroundService
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
        if (intervalHours is < 1 or > 720) throw new InvalidOperationException("Invalid GPS retention run interval.");
        health?.Register("gps-retention", TimeSpan.FromHours(intervalHours + 1));
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
                    health?.Complete("gps-retention");
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception)
                {
                    health?.Fail("gps-retention");
                    logger.LogError("GPS punch retention failed; review protected operational diagnostics.");
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
