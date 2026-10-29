using Cosmix.Application.Authentication;

namespace Cosmix.Api.Authentication;

public sealed class OtpDeliveryWorker(IServiceScopeFactory scopes, MobileAuthenticationOptions options,
    ILogger<OtpDeliveryWorker> logger, Cosmix.Application.Common.BackgroundJobs.WorkerHealth? health = null) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Enabled) return;
        health?.Register("otp-dispatch", TimeSpan.FromSeconds(60)); 
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));
        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                try
                {
                    using var scope = scopes.CreateScope();
                    await scope.ServiceProvider.GetRequiredService<OtpDispatchService>().DispatchOneAsync(stoppingToken);
                    health?.Complete("otp-dispatch");
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
                catch (Exception exception)
                {
                    health?.Fail("otp-dispatch");
                    logger.LogWarning("OTP dispatcher unavailable ({ExceptionType}); no delivery success assumed.", exception.GetType().Name);
                    await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
    }
}
