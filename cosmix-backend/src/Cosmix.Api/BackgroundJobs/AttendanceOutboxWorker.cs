using Cosmix.Application.Common.BackgroundJobs;

namespace Cosmix.Api.BackgroundJobs;

public sealed class AttendanceOutboxWorker(IServiceScopeFactory scopes, AttendanceJobOptions options,
    ILogger<AttendanceOutboxWorker> logger, Cosmix.Application.Common.BackgroundJobs.WorkerHealth? health = null) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Enabling requires the correct database/storage and authorized operating setup. Registration alone writes nothing.
        if (!options.Enabled) return;
        health?.Register("outbox", TimeSpan.FromSeconds(Math.Max(120, options.PollSeconds * 3))); 
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(options.PollSeconds));
        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                try
                {
                    for (var i = 0; i < options.BatchSize; i++)
                    {
                        using var scope = scopes.CreateScope();
                        if (!await scope.ServiceProvider.GetRequiredService<DurableJobProcessor>().DispatchOneAsync(stoppingToken)) break;
                    }
                    health?.Complete("outbox");
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
                catch (Exception error)
                {
                    health?.Fail("outbox");
                    logger.LogWarning("Attendance outbox unavailable ({ExceptionType}); no dispatch success assumed.", error.GetType().Name);
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
    }
}
