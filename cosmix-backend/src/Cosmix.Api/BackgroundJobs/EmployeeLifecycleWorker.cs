using Cosmix.Application.HR.Employees;

namespace Cosmix.Api.BackgroundJobs;

public sealed class EmployeeLifecycleWorker(IServiceScopeFactory scopes, ILogger<EmployeeLifecycleWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Operational polling interval only: access is denied from EffectiveAt by live authentication checks.
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(30));
        long afterEmployeeNumber = 0;
        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                try
                {
                    using var scope = scopes.CreateScope();
                    var batch = await scope.ServiceProvider.GetRequiredService<EmployeeLifecycleProcessor>()
                        .ReconcileBatchAsync(afterEmployeeNumber, stoppingToken);
                    afterEmployeeNumber = batch.NextEmployeeNumber;
                    if (batch.ReviewRequired > 0)
                        logger.LogWarning("{Count} employee lifecycle records require review; continuing the remaining sweep.", batch.ReviewRequired);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
                catch (Exception error)
                {
                    logger.LogWarning("Employee lifecycle reconciliation unavailable ({ExceptionType}); effective-status access checks remain enforced.", error.GetType().Name);
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
    }
}
