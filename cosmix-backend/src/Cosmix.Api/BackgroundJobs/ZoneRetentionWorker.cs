using Cosmix.Application.Attendance.Zones;
using Cosmix.Application.Authentication;
using Cosmix.Infrastructure.Zones;

namespace Cosmix.Api.BackgroundJobs;

/// <summary>Product retention worker; never enables monitoring or provisions storage/settings.</summary>
public sealed class ZoneRetentionWorker(IServiceScopeFactory scopes,ZoneRuntimeOptions options,IZoneLocationStore files,
    ZoneRetentionHealth health,TimeProvider clock,ILogger<ZoneRetentionWorker> logger):BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if(!options.RetentionEnabled || !files.IsConfigured) return;
        string? cursor=null; var invalidSweep=false;
        while(!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var invalid=0; var pendingEvents=false;
                for(var page=0;page<10;page++)
                {
                    await using var scope=scopes.CreateAsyncScope();
                    var result=await scope.ServiceProvider.GetRequiredService<ZoneRetentionProcessor>().RunAsync(cursor,stoppingToken);
                    invalid+=result.InvalidEntries; cursor=result.NextCursor; pendingEvents=result.DeletedEvents==100;
                    if(cursor is null && !pendingEvents) break;
                }
                invalidSweep|=invalid!=0;
                if(cursor is null && !pendingEvents)
                {
                    if(!invalidSweep) health.Complete(clock.GetUtcNow());
                    else { health.Fail(); logger.LogWarning("Zone retention has invalid owned objects; monitoring remains unavailable."); }
                    invalidSweep=false;
                }
                else { health.Fail(); logger.LogWarning("Zone retention has pending owned objects; monitoring remains unavailable."); }
            }
            catch(OperationCanceledException) when(stoppingToken.IsCancellationRequested) { break; }
            catch(AuthenticationFailure error) when(error.Code=="zone_retention_cursor_expired")
            { health.Fail(); cursor=null; invalidSweep=false; }
            catch(Exception)
            {
                health.Fail(); invalidSweep=true;
                // Never log exception payloads, location, references, keys, or private paths.
                logger.LogWarning("Zone retention is unavailable; monitoring remains unavailable.");
            }
            await Task.Delay(TimeSpan.FromSeconds(30),clock,stoppingToken);
        }
        health.Fail();
    }
}
