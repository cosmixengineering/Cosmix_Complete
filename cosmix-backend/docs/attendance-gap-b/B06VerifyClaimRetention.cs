#:project ../../src/Cosmix.Infrastructure/Cosmix.Infrastructure.csproj
#:property PublishAot=false
#:property RestorePackagesWithLockFile=false
using System.Reflection;
using Cosmix.Application.Attendance.Zones;
using Cosmix.Application.Authentication;
using Cosmix.Infrastructure.Zones;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

var checks = 0; var ct = CancellationToken.None;
void Check(bool pass, string name) { if (!pass) throw new Exception("FAIL: " + name); checks++; }
async Task Fails(Func<Task> action, string code)
{
    try { await action(); throw new Exception("Expected " + code); }
    catch (AuthenticationFailure error) { Check(error.Code == code, $"Expected {code}, got {error.Code}"); }
}
var clock = new RetentionClock();
ZoneClaimIndexEntry Entry() => new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), clock.Now.AddSeconds(-1), "synthetic-reference");
var refused = new RetentionStore { DeleteResult = false }; refused.Pages.Enqueue(new([Entry()], null, 0));
await Fails(() => new ZoneClaimRetentionService(refused, clock).PurgeAsync(null, 100, ct), "zone_claim_storage_unavailable");
Check(refused.Deletes == 1, "refused expiry deletion is not a completed purge");

var method = typeof(ZoneClaimRetentionWorker).GetMethod("RunPassAsync", BindingFlags.Instance | BindingFlags.NonPublic);
Check(method is not null, "scheduled worker exposes its actual bounded pass without a host/background activation");
async Task Pass(ZoneClaimRetentionWorker worker, CancellationToken token = default)
{
    try { await (Task)method!.Invoke(worker, [token])!; }
    catch (TargetInvocationException e) when (e.InnerException is OperationCanceledException) { throw e.InnerException; }
}
WorkerFixture Worker(RetentionStore store, bool enabled = true) => new(store, clock, new() { Enabled = enabled });

var defaults = new RetentionStore(); using (var w = Worker(defaults, false))
{
    w.Health.Complete(clock.Now); await Pass(w.Worker);
    Check(defaults.Scans == 0 && !w.Health.IsAvailable(clock.Now), "disabled defaults never scan or preserve stale healthy status");
}
var absent = new RetentionStore { IsConfigured = false }; using (var w = Worker(absent))
{
    await Pass(w.Worker); Check(absent.Scans == 0 && !w.Health.IsAvailable(clock.Now), "unconfigured claim storage cannot activate cleanup/health");
}
var clean = new RetentionStore(); clean.Pages.Enqueue(new([Entry()], null, 0)); using (var w = Worker(clean))
{
    await Pass(w.Worker);
    Check(clean.Scans == 1 && clean.Deletes == 1 && w.Health.IsAvailable(clock.Now), "one actual scoped cleanup pass publishes health only after successful configured expiry");
    Check(clean.Requests.All(x => x.Maximum == 100) && clean.DeleteCutoffs.All(x => x == clock.Now), "worker uses bounded scan and actual UTC cutoff, not a retention grace");
    Check(!w.Health.IsAvailable(clock.Now.AddTicks(-1)) && !w.Health.IsAvailable(clock.Now.AddMinutes(2).AddTicks(1)), "health is process freshness, not report-retention authority");
}
var incomplete = new RetentionStore { DeleteResult = false }; incomplete.Pages.Enqueue(new([Entry()], null, 0)); using (var w = Worker(incomplete))
{
    await Pass(w.Worker); Check(!w.Health.IsAvailable(clock.Now), "refused object deletion keeps real worker health unavailable");
}
var pending = new RetentionStore(); var cursor = Guid.NewGuid().ToString("N");
for (var i = 0; i < 10; i++) pending.Pages.Enqueue(new([], cursor, 0));
pending.Pages.Enqueue(new([], null, 0)); using (var w = Worker(pending))
{
    await Pass(w.Worker);
    Check(pending.Scans == 10 && !w.Health.IsAvailable(clock.Now), "pass is capped at ten 100-entry pages and cannot publish health with pending cursor");
    await Pass(w.Worker);
    Check(pending.Scans == 11 && pending.Requests[10].Cursor == cursor && w.Health.IsAvailable(clock.Now), "next bounded pass continues original cursor and completes health only at sweep end");
}
var invalid = new RetentionStore(); invalid.Pages.Enqueue(new([], cursor, 1)); invalid.Pages.Enqueue(new([], null, 0));
invalid.Pages.Enqueue(new([], null, 0)); using (var w = Worker(invalid))
{
    await Pass(w.Worker); Check(!w.Health.IsAvailable(clock.Now), "invalid allocation remains a failed entire sweep, even when final page is clean");
    await Pass(w.Worker); Check(w.Health.IsAvailable(clock.Now), "only a subsequent complete clean sweep restores health");
}
var expiredCursor = new RetentionStore(); expiredCursor.Pages.Enqueue(new([], cursor, 0));
expiredCursor.Failures.Enqueue(null); expiredCursor.Failures.Enqueue(new AuthenticationFailure("zone_claim_retention_cursor_expired", 409));
using (var w = Worker(expiredCursor))
{
    await Pass(w.Worker); Check(!w.Health.IsAvailable(clock.Now), "expired scan cursor fails closed");
    await Pass(w.Worker); Check(expiredCursor.Requests.Last().Cursor is null && w.Health.IsAvailable(clock.Now), "expired cursor restarts a bounded sweep rather than trusting prior completion");
}
var failing = new RetentionStore(); failing.Failures.Enqueue(new IOException("PRIVATE_CLAIM PRIVATE_KEY PRIVATE_PATH"));
using (var w = Worker(failing))
{
    await Pass(w.Worker); Check(!w.Health.IsAvailable(clock.Now), "storage failure immediately denies healthy cleanup");
    Check(w.Logger.Messages.All(x => !x.Contains("PRIVATE_")) && w.Logger.Exceptions.All(x => x is null), "worker warnings contain no private payload/path/key or exception detail");
}
var cancelled = new RetentionStore(); using (var w = Worker(cancelled)) using (var stop = new CancellationTokenSource())
{
    w.Health.Complete(clock.Now); stop.Cancel();
    try { await Pass(w.Worker, stop.Token); throw new Exception("Ignored cancellation"); }
    catch (OperationCanceledException) { Check(!w.Health.IsAvailable(clock.Now) && cancelled.Scans == 0, "cancellation clears health and performs no scan"); }
}
Console.WriteLine($"PASS: {checks} B06 actual bounded worker/cleanup/health checks; direct passes only, no host start, files, SQL, provider or policy activation.");

sealed class RetentionClock : TimeProvider { public DateTimeOffset Now = new(2026, 10, 13, 10, 0, 0, TimeSpan.Zero); public override DateTimeOffset GetUtcNow() => Now; }
sealed class RetentionStore : IZoneClaimStore
{
    public bool IsConfigured { get; set; } = true;
    public bool DeleteResult = true;
    public int Scans, Deletes;
    public Queue<ZoneClaimIndexPage> Pages = [];
    public Queue<Exception?> Failures = [];
    public List<(string? Cursor, int Maximum)> Requests = [];
    public List<DateTimeOffset> DeleteCutoffs = [];
    public Task<ZoneClaimIndexPage> ScanExpiredAsync(DateTimeOffset now, string? cursor, int maximum, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested(); Scans++; Requests.Add((cursor, maximum));
        if (Failures.TryDequeue(out var error) && error is not null) throw error;
        return Task.FromResult(Pages.TryDequeue(out var page) ? page : new ZoneClaimIndexPage([], null, 0));
    }
    public Task<bool> DeleteExpiredAsync(Guid id, Guid employee, Guid window, DateTimeOffset expiry, string reference, DateTimeOffset now, CancellationToken ct)
    { ct.ThrowIfCancellationRequested(); if (now < expiry) throw new Exception("Early deletion"); Deletes++; DeleteCutoffs.Add(now); return Task.FromResult(DeleteResult); }
    public Task<string> StoreAsync(ZoneClaimPayload envelope, CancellationToken ct) => throw new Exception("Cleanup cannot receive claims");
    public Task<ZoneClaimPayload> ReadAsync(Guid id, Guid employee, Guid window, DateTimeOffset expiry, string reference, DateTimeOffset now, CancellationToken ct) => throw new Exception("Cleanup cannot expose claims");
}
sealed class WorkerFixture : IDisposable
{
    private readonly ServiceProvider provider;
    public readonly ZoneClaimRetentionHealth Health = new();
    public readonly RetentionLogger Logger = new();
    public readonly ZoneClaimRetentionWorker Worker;
    public WorkerFixture(IZoneClaimStore store, TimeProvider clock, ZoneOfflineObservationOptions options)
    {
        var services = new ServiceCollection(); services.AddSingleton(store); services.AddSingleton(clock); services.AddScoped<ZoneClaimRetentionService>();
        provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
        Worker = new(provider.GetRequiredService<IServiceScopeFactory>(), options, store, Health, clock, Logger);
    }
    public void Dispose() { Worker.Dispose(); provider.Dispose(); }
}
sealed class RetentionLogger : ILogger<ZoneClaimRetentionWorker>
{
    public List<string> Messages = []; public List<Exception?> Exceptions = [];
    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
    public bool IsEnabled(LogLevel level) => true;
    public void Log<TState>(LogLevel level, EventId id, TState state, Exception? error, Func<TState, Exception?, string> formatter)
    { Messages.Add(formatter(state, error)); Exceptions.Add(error); }
}
