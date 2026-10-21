using Cosmix.Application.Attendance.Zones;
using Cosmix.Application.Authentication;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Cosmix.Infrastructure.Zones;

public sealed class ZoneClaimRetentionHealth : IZoneClaimRetentionStatus
{
    private long lastCompleteTicks;
    public bool IsAvailable(DateTimeOffset now)
    {
        var complete = Interlocked.Read(ref lastCompleteTicks);
        return complete > 0 && now.UtcTicks >= complete && now.UtcTicks - complete <= TimeSpan.FromMinutes(2).Ticks;
    }
    public void Complete(DateTimeOffset now) => Interlocked.Exchange(ref lastCompleteTicks, now.UtcTicks);
    public void Fail() => Interlocked.Exchange(ref lastCompleteTicks, 0);
}

/// <summary>Feature-local configured claim purge. Never provisions storage or chooses report retention.</summary>
public sealed class ZoneClaimRetentionWorker(IServiceScopeFactory scopes, ZoneOfflineObservationOptions options,
    IZoneClaimStore store, ZoneClaimRetentionHealth health, TimeProvider clock, ILogger<ZoneClaimRetentionWorker> logger) : BackgroundService
{
    private string? cursor;
    private bool invalidSweep;

    internal async Task RunPassAsync(CancellationToken ct)
    {
        if (!options.Enabled || !store.IsConfigured)
        { health.Fail(); cursor = null; invalidSweep = false; return; }
        try
        {
            ct.ThrowIfCancellationRequested();
            for (var page = 0; page < 10; page++)
            {
                await using var scope = scopes.CreateAsyncScope();
                var result = await scope.ServiceProvider.GetRequiredService<ZoneClaimRetentionService>().PurgeAsync(cursor, 100, ct);
                cursor = result.NextCursor; invalidSweep |= result.InvalidEntries != 0;
                if (cursor is null) break;
            }
            if (cursor is null && !invalidSweep) health.Complete(clock.GetUtcNow());
            else { health.Fail(); logger.LogWarning("Zone claim retention is incomplete; offline intake remains unavailable."); }
            if (cursor is null) invalidSweep = false;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { health.Fail(); throw; }
        catch (AuthenticationFailure error) when (error.Code == "zone_claim_retention_cursor_expired")
        { health.Fail(); cursor = null; invalidSweep = false; }
        catch (Exception)
        {
            health.Fail(); invalidSweep = true;
            logger.LogWarning("Zone claim retention is unavailable; offline intake remains unavailable.");
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Enabled || !store.IsConfigured) { health.Fail(); return; }
        while (!stoppingToken.IsCancellationRequested)
        {
            try { await RunPassAsync(stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            try { await Task.Delay(TimeSpan.FromSeconds(30), clock, stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
        }
        health.Fail();
    }
}
