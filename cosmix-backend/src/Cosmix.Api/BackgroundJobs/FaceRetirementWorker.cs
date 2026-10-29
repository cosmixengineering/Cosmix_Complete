using Cosmix.Application.Attendance.FaceVerification;

namespace Cosmix.Api.BackgroundJobs;

/// <summary>Disabled by default. Never enables providers or provisions disposal authority.</summary>
public sealed class FaceRetirementWorker(IServiceScopeFactory scopes, FaceRetirementWorkerOptions options,
    TimeProvider clock, ILogger<FaceRetirementWorker> logger,
    Cosmix.Application.Common.BackgroundJobs.WorkerHealth? health = null) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Enabled) return;
        health?.Register("face-retirement", TimeSpan.FromSeconds(Math.Clamp(options.PollSeconds, 1, 3600) * 3 + 60));
        if (options.BatchSize is < 1 or > 100 || options.PollSeconds is < 1 or > 3600)
        { health?.Fail("face-retirement"); logger.LogWarning("Face retirement worker configuration is unavailable."); return; }
        FaceRetirementCursor? cursor = null; FaceRetirementCursor? revocationCursor = null;
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                var result = await scope.ServiceProvider.GetRequiredService<FaceRetirementProcessor>()
                    .RunAsync(cursor, options.BatchSize, stoppingToken, revocationCursor);
                cursor = result.NextCursor;
                revocationCursor = result.NextRevocationCursor;
                if (result.RecoveryPending != 0 || result.RecoveryFailed != 0 || result.Failed != 0 || result.Pending != 0)
                    health?.Fail("face-retirement");
                else health?.Complete("face-retirement");
                if (result.RecoveryPending != 0 || result.RecoveryFailed != 0)
                    logger.LogWarning("Face retirement recovery has {Pending} authority-pending and {Failed} failed revoked sources.",
                        result.RecoveryPending, result.RecoveryFailed);
                if (result.Failed != 0) logger.LogWarning("Face retirement processing has {Count} retry or scope failures.", result.Failed);
                else if (result.Examined != 0) logger.LogInformation("Face retirement examined {Count}; use closed {Closed}; disposal or terminal pending {Pending}.",
                    result.Examined, result.UseClosed, result.Pending);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception)
            {
                health?.Fail("face-retirement");
                // No exception text, employee identity, private path, storage reference or tokens.
                logger.LogWarning("Face retirement processing is unavailable; durable sources remain pending.");
                cursor = null;
                revocationCursor = null;
            }
            try { await Task.Delay(TimeSpan.FromSeconds(options.PollSeconds), clock, stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
        }
    }
}
