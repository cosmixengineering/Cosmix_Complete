using System.Data.Common;
using System.Reflection;
using Cosmix.Application.Common.BackgroundJobs;
using Cosmix.Domain.Common.Audit;
using Cosmix.Domain.Common.Jobs;
using Cosmix.Infrastructure.BackgroundJobs;
using Cosmix.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

var passed = 0; var failures = new List<string>();
void Check(bool value, string message) { if (!value) throw new Exception(message); }
async Task Test(string name, Func<Task> action)
{
    try { await action(); passed++; Console.WriteLine("PASS " + name); }
    catch (Exception e) { failures.Add(name + ": " + e.GetBaseException().Message); Console.WriteLine("FAIL " + failures[^1]); }
}

await Test("ordinary handler and four-argument claim stay compatible", async () => {
    var f = new JobFixture(false, false); Check(await f.Dispatch(), "Ordinary work completed");
    Check(f.Handler.Prepares == 0 && f.Handler.Applies == 1 && f.Jobs.Message.State == DispatchState.Completed && f.Jobs.Audits.Count == 1,
        "Ordinary handler applies atomically without metadata");
});
await Test("ordinary retry behavior stays compatible", async () => {
    var f = new JobFixture(false, false); f.Handler.Apply = (_, _) => throw new DurableJobFailure("ordinary_retry", true);
    Check(await f.Dispatch(), "Failure handled");
    Check(f.Jobs.Message.State == DispatchState.Pending && f.Jobs.Message.LastFailureCode == "ordinary_retry" &&
        f.Jobs.Message.AvailableAt == f.Clock.Now.AddSeconds(5) && f.Jobs.Audits.Count == 0, "Existing retry policy");
});
await Test("prepared handler prepares outside completion and applies inside", async () => {
    var f = new JobFixture(); Check(await f.Dispatch(), "Prepared work completed");
    Check(f.Handler.Prepares == 1 && f.Handler.Applies == 1 && f.Jobs.Message.State == DispatchState.Completed && f.Jobs.Audits.Count == 1,
        "Prepared work retains ordinary atomic completion");
});
await Test("prepared handler without metadata fails closed", async () => {
    var f = new JobFixture(true, false); await f.Dispatch();
    Check(f.Handler.Prepares == 0 && f.Handler.Applies == 0 && f.Jobs.Message.State == DispatchState.Failed &&
        f.Jobs.Message.LastFailureCode == "dispatch_preparation_missing", "No prepared apply without bound metadata");
});
foreach (var field in new[] { "Id", "MessageType", "EncryptedPayloadReference", "CorrelationId", "DeduplicationKey", "Attempts", "handler", "claim" })
await Test("prepared completion rejects mutated " + field, async () => {
    var f = new JobFixture();
    f.Handler.Prepare = (claim, _) => {
        if (field == "handler") f.Handler.TypeCode = "prepared.changed";
        else if (field == "claim") JobFixture.SetPreparation(claim, f.Jobs.Message, "changed-payload");
        else typeof(OutboxMessage).GetProperty(field)!.SetValue(f.Jobs.Message, field == "Id" ? Guid.NewGuid() : field == "Attempts" ? 99 : "changed");
        return Task.CompletedTask;
    };
    await f.Dispatch();
    Check(f.Handler.Prepares == 1 && f.Handler.Applies == 0 && f.Jobs.Audits.Count == 0, "Changed binding cannot apply or audit success");
});
await Test("prepared message cannot switch to another registered ordinary handler", async () => {
    var f = new JobFixture(); var ordinary = new OrdinaryJobHandler(f.Jobs) { TypeCode = "ordinary.other" };
    f.Handler.Prepare = (_, _) => { typeof(OutboxMessage).GetProperty("MessageType")!.SetValue(f.Jobs.Message, ordinary.MessageType); return Task.CompletedTask; };
    await new DurableJobProcessor(f.Jobs, [f.Registered, ordinary], f.Options, f.Clock).DispatchOneAsync(default);
    Check(f.Handler.Applies == 0 && ordinary.Applies == 0 && f.Jobs.Audits.Count == 0, "Prepared output cannot cross handlers");
});
await Test("missing and empty nullable metadata values are not equivalent", async () => {
    var f = new JobFixture(); f.Jobs.AfterClaim = claim => JobFixture.SetPreparation(claim, f.Jobs.Message, deduplication: null, replaceDeduplication: true);
    await f.Dispatch(); Check(f.Handler.Applies == 0 && f.Jobs.Message.LastFailureCode == "dispatch_preparation_mismatch", "Exact nullable key comparison");
});
await Test("prepare failure never enters completion and uses bounded retry", async () => {
    var f = new JobFixture(); f.Handler.Prepare = (_, _) => throw new DurableJobFailure("prepared_io_retry", true);
    await f.Dispatch(); var firstLease = f.Handler.Claims.Single().LeaseToken;
    Check(f.Jobs.Completes == 0 && f.Handler.Applies == 0 && f.Jobs.Message.State == DispatchState.Pending &&
        f.Jobs.Message.AvailableAt == f.Clock.Now.AddSeconds(5), "Failed preparation creates no business apply");
    f.Clock.Now = f.Jobs.Message.AvailableAt; f.Handler.Prepare = null;
    await f.Dispatch();
    Check(f.Handler.Prepares == 2 && f.Handler.Claims.Last().LeaseToken != firstLease && f.Handler.Claims.Last().Attempt == 2 &&
        f.Handler.Applies == 1 && f.Jobs.Message.State == DispatchState.Completed, "Retry prepares a fresh lease and completes once");
});
await Test("maximum attempts stops preparation as well as apply", async () => {
    var f = new JobFixture(); typeof(OutboxMessage).GetProperty("Attempts")!.SetValue(f.Jobs.Message, f.Options.MaximumAttempts);
    await f.Dispatch(); Check(f.Handler.Prepares == 0 && f.Handler.Applies == 0 && f.Jobs.Message.State == DispatchState.Failed &&
        f.Jobs.Message.LastFailureCode == "dispatch_attempts_exhausted", "No extra preparation beyond attempts budget");
});
await Test("expired lease after preparation never applies", async () => {
    var f = new JobFixture(); f.Handler.Prepare = (claim, _) => { f.Clock.Now = claim.LeaseExpiresAt; return Task.CompletedTask; };
    Check(!await f.Dispatch(), "Expired completion does not succeed");
    Check(f.Handler.Prepares == 1 && f.Handler.Applies == 0 && f.Jobs.Message.State == DispatchState.Processing && f.Jobs.Audits.Count == 0,
        "Expired lease is left for reclaim without business changes");
});
await Test("caller cancellation during preparation is propagated without recording failure", async () => {
    var f = new JobFixture(); using var cts = new CancellationTokenSource();
    f.Handler.Prepare = (_, token) => { cts.Cancel(); token.ThrowIfCancellationRequested(); return Task.CompletedTask; };
    try { await f.Dispatch(cts.Token); throw new Exception("Cancellation was swallowed"); } catch (OperationCanceledException) when (cts.IsCancellationRequested) { }
    Check(f.Handler.Applies == 0 && f.Jobs.Completes == 0 && f.Jobs.Fails == 0 && f.Jobs.Message.State == DispatchState.Processing,
        "Cancellation retains lease retry semantics");
});
await Test("cancellation ignored by preparation still cannot apply", async () => {
    var f = new JobFixture(); using var cts = new CancellationTokenSource();
    f.Handler.Prepare = (_, _) => { cts.Cancel(); return Task.CompletedTask; };
    try { await f.Dispatch(cts.Token); throw new Exception("Cancellation was swallowed"); } catch (OperationCanceledException) when (cts.IsCancellationRequested) { }
    Check(f.Handler.Applies == 0 && f.Jobs.Fails == 0, "Dispatcher checks cancellation before completion");
});
await Test("completion authority reload remains decisive after successful preparation", async () => {
    var f = new JobFixture(); var writable = true; var staged = 0;
    f.Handler.Prepare = (_, _) => { writable = false; return Task.CompletedTask; };
    f.Handler.Apply = (_, _) => { if (!writable) throw new DurableJobFailure("revoked_current_authority", false); staged++; return Task.CompletedTask; };
    await f.Dispatch(); Check(f.Handler.Prepares == 1 && f.Handler.Applies == 1 && staged == 0 && f.Jobs.Message.State == DispatchState.Failed &&
        f.Jobs.Audits.Count == 0, "Prepared data cannot bypass authoritative Apply validation");
});
await Test("ordinary handler still applies with real claim metadata", async () => {
    var f = new JobFixture(false, true); Check(await f.Dispatch(), "Ordinary metadata-bearing claim completes");
    Check(f.Handler.Prepares == 0 && f.Handler.Applies == 1 && f.Jobs.Message.State == DispatchState.Completed, "Preparation is opt-in");
});
await Test("actual SQL candidate translation retains exact ownership lease filters and metadata", () => {
    var guard = new NoJobConnections();
    using var db = new CosmixDbContext(new DbContextOptionsBuilder<CosmixDbContext>()
        .UseSqlServer("Server=127.0.0.1,1;Database=NeverConnect;User ID=none;Password=unused;TrustServerCertificate=True")
        .AddInterceptors(guard).Options);
    var sql = DurableJobRepository.ClaimCandidates(db.OutboxMessages, ["prepared.example", "ordinary.example"], new JobClock().Now).ToQueryString();
    Check(new[] { "MessageType", "EncryptedPayloadReference", "CorrelationId", "DeduplicationKey", "LeaseExpiresAt", "AvailableAt", "State", "Latin1_General_100_BIN2" }
        .All(sql.Contains) && sql.Contains("prepared.example|") && guard.Attempts == 0, "Offline real repository SQL selects bound metadata and exact registered ownership");
    return Task.CompletedTask;
});
Console.WriteLine($"Prepared durable jobs: {passed}/{passed + failures.Count} scenarios. No host or SQL connections.");
if (failures.Count > 0) Environment.ExitCode = 1;

public sealed class JobClock : TimeProvider
{
    public DateTimeOffset Now = new(2026, 10, 19, 7, 0, 0, TimeSpan.Zero);
    public override DateTimeOffset GetUtcNow() => Now;
}
public class PreparedJobProxy : DispatchProxy
{
    public JobHandlerState State = null!;
    protected override object? Invoke(MethodInfo? method, object?[]? args) => method!.Name switch {
        "get_MessageType" => State.TypeCode,
        "PrepareAsync" => State.PrepareAsync((DurableJobClaim)args![0]!, (CancellationToken)args[1]!),
        "ApplyAsync" => State.ApplyAsync((OutboxMessage)args![0]!, (CancellationToken)args[1]!),
        _ => throw new Exception("Unexpected handler member " + method.Name) };
}
public sealed class JobHandlerState(JobRepository jobs)
{
    public string TypeCode = "prepared.example";
    public int Prepares, Applies;
    public List<DurableJobClaim> Claims = [];
    public Func<DurableJobClaim, CancellationToken, Task>? Prepare;
    public Func<OutboxMessage, CancellationToken, Task>? Apply;
    public async Task PrepareAsync(DurableJobClaim claim, CancellationToken ct)
    {
        if (jobs.InCompletion) throw new Exception("Preparation entered completion transaction");
        Prepares++; Claims.Add(claim); if (Prepare is not null) await Prepare(claim, ct);
    }
    public async Task ApplyAsync(OutboxMessage message, CancellationToken ct)
    {
        if (!jobs.InCompletion) throw new Exception("Apply escaped completion transaction");
        Applies++; if (Apply is not null) await Apply(message, ct);
    }
}
public sealed class OrdinaryJobHandler(JobRepository jobs) : IDurableJobHandler
{
    public string TypeCode = "ordinary.example"; public int Applies;
    public JobHandlerState? State;
    public string MessageType => State?.TypeCode ?? TypeCode;
    public Task ApplyAsync(OutboxMessage message, CancellationToken ct)
    { if (!jobs.InCompletion) throw new Exception("Ordinary apply outside completion"); Applies++; return State?.ApplyAsync(message, ct) ?? Task.CompletedTask; }
}
public sealed class JobFixture
{
    public readonly JobClock Clock = new();
    public readonly AttendanceJobOptions Options = new() { Enabled = true, MaximumAttempts = 3, LeaseSeconds = 30, RetryBaseSeconds = 5 };
    public readonly JobRepository Jobs;
    public readonly JobHandlerState Handler;
    public readonly IDurableJobHandler Registered;
    public JobFixture(bool prepared = true, bool metadata = true)
    {
        Jobs = new(Clock, metadata); Handler = new(Jobs);
        if (prepared)
        {
            var contract = typeof(DurableJobProcessor).Assembly.GetType("Cosmix.Application.Common.BackgroundJobs.IPreparedDurableJobHandler")
                ?? throw new Exception("Missing IPreparedDurableJobHandler contract");
            var proxy = DispatchProxy.Create(contract, typeof(PreparedJobProxy)); ((PreparedJobProxy)proxy).State = Handler;
            Registered = (IDurableJobHandler)proxy;
        }
        else Registered = new OrdinaryJobHandler(Jobs) { State = Handler };
    }
    public Task<bool> Dispatch(CancellationToken ct = default) => new DurableJobProcessor(Jobs, [Registered], Options, Clock).DispatchOneAsync(ct);
    public static void SetPreparation(DurableJobClaim claim, OutboxMessage message, string? payload = null, string? deduplication = null, bool replaceDeduplication = false)
    {
        var property = typeof(DurableJobClaim).GetProperty("Preparation") ?? throw new Exception("Missing claim preparation metadata");
        var value = Activator.CreateInstance(property.PropertyType, message.MessageType, payload ?? message.EncryptedPayloadReference,
            message.CorrelationId, replaceDeduplication ? deduplication : message.DeduplicationKey);
        property.SetValue(claim, value);
    }
}
public sealed class JobRepository(JobClock clock, bool metadata) : IDurableJobRepository
{
    public OutboxMessage Message = new("prepared.example", "protected-payload", clock.Now, "correlation", "deduplication");
    public bool InCompletion;
    public int Completes, Fails;
    public List<AuditEvent> Audits = [];
    public Action<DurableJobClaim>? AfterClaim;
    public Task<DurableJobClaim?> ClaimNextAsync(IReadOnlyCollection<string> allowed, DateTimeOffset now, TimeSpan lease, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested(); if (!allowed.Contains(Message.MessageType)) throw new Exception("Missing exact ownership");
        var token = Message.Claim(now, now.Add(lease)); var claim = new DurableJobClaim(Message.Id, token, Message.LeaseExpiresAt!.Value, Message.Attempts);
        if (metadata) JobFixture.SetPreparation(claim, Message); AfterClaim?.Invoke(claim); return Task.FromResult<DurableJobClaim?>(claim);
    }
    public async Task<bool> CompleteAsync(DurableJobClaim claim, Func<OutboxMessage, CancellationToken, Task> action, CancellationToken ct)
    {
        Completes++; if (!Owns(claim)) return false; InCompletion = true;
        try { await action(Message, ct); if (!Owns(claim)) throw new DurableJobFailure("dispatch_lease_expired", true); Message.Complete(claim.LeaseToken, clock.Now); return true; }
        catch { Audits.Clear(); throw; }
        finally { InCompletion = false; }
    }
    public Task<bool> FailAsync(DurableJobClaim claim, string code, DateTimeOffset? retryAt, CancellationToken ct)
    { Fails++; if (!Owns(claim)) return Task.FromResult(false); Message.RecordFailure(claim.LeaseToken, clock.Now, code, retryAt); return Task.FromResult(true); }
    private bool Owns(DurableJobClaim claim) => Message.State == DispatchState.Processing && Message.LeaseToken == claim.LeaseToken &&
        Message.LeaseExpiresAt == claim.LeaseExpiresAt && Message.LeaseExpiresAt > clock.Now;
    public void AddAudit(AuditEvent audit) { if (!InCompletion) throw new Exception("Audit outside completion"); Audits.Add(audit); }
    public void RequireProducerTransaction() => throw new NotSupportedException();
    public Task<OutboxMessage?> FindByIdentityAsync(string type, string key, CancellationToken ct) => throw new NotSupportedException();
    public Task<bool> RecipientExistsAsync(Guid id, CancellationToken ct) => throw new NotSupportedException();
    public void Stage(OutboxMessage message) => throw new NotSupportedException();
}
public sealed class NoJobConnections : DbConnectionInterceptor
{
    public int Attempts;
    public override InterceptionResult ConnectionOpening(DbConnection connection, ConnectionEventData eventData, InterceptionResult result)
    { Attempts++; throw new Exception("SQL connections forbidden"); }
    public override ValueTask<InterceptionResult> ConnectionOpeningAsync(DbConnection connection, ConnectionEventData eventData,
        InterceptionResult result, CancellationToken cancellationToken = default)
    { Attempts++; throw new Exception("SQL connections forbidden"); }
}
