using Cosmix.Application.Attendance.Reminders;
using Cosmix.Application.Common.BackgroundJobs;

namespace Cosmix.Api.BackgroundJobs;

public sealed class MissingCheckoutReminderWorker(IServiceScopeFactory scopes, MissingCheckoutReminderOptions options,
    AttendanceJobOptions delivery, ILogger<MissingCheckoutReminderWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Enabled || !delivery.Enabled) return;
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(options.PollSeconds));
        long afterEmployeeNumber = 0;
        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                try
                {
                    using var scope = scopes.CreateScope();
                    var batch = await scope.ServiceProvider.GetRequiredService<MissingCheckoutReminderProcessor>()
                        .RunBatchAsync(afterEmployeeNumber, stoppingToken);
                    afterEmployeeNumber = batch.NextEmployeeNumber;
                    if (batch.ReviewRequired > 0)
                        logger.LogWarning("{Count} missing-checkout reminder sources require review; sweep continues.", batch.ReviewRequired);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
                catch (Exception error)
                {
                    logger.LogWarning("Missing-checkout reminders unavailable ({ExceptionType}); no reminder success assumed.", error.GetType().Name);
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
    }
}
