#:project ../../src/Cosmix.Application/Cosmix.Application.csproj
#:property PublishAot=false
#:property RestorePackagesWithLockFile=false
#:property NuGetLockFilePath=VerifyEnrollmentJournal.packages.lock.json
using System.Security.Cryptography;
using System.Text;
using Cosmix.Application.Attendance.Common;
using Cosmix.Application.Attendance.FaceVerification;
using Cosmix.Application.Authentication;
using Cosmix.Application.Authentication.Access;
using Cosmix.Application.Common.Files;
using Cosmix.Domain.Authentication;
using Cosmix.Domain.Common.Audit;
using Cosmix.Domain.Common.Idempotency;

// Real journal; serialized in-memory transaction double. No SQL atomicity/provider acceptance claim.
var checks = 0;
void Check(bool valid, string name) { checks++; if (!valid) throw new Exception(name); }
async Task Deny(Func<Task> action, string code)
{ checks++; try { await action(); } catch (AuthenticationFailure e) when (e.Code == code) { return; } throw new Exception(code); }
var f = new JournalFixture();
var start = await f.Start("attempt-key-001");
Check(start.Created && start.Handle is not null && f.Repository.Receipts.Count == 1, "one immutable authorized start");
var repeated = await f.Start("attempt-key-001");
Check(!repeated.Created && repeated.Handle is null && repeated.Terminal is null, "in-flight retry cannot obtain provider finalization proof or start twice");
Check(f.Files.Objects.Values.All(x => !Encoding.UTF8.GetString(x.Content).Contains(start.Handle!.FinalizationProof, StringComparison.Ordinal)), "process proof never retained as plaintext");
await Deny(() => f.Start("attempt-key-001", new string('B', 64)), "idempotency_key_reused");
await Deny(() => f.Journal.CompleteFailureAsync(new(start.Start.ReceiptId, "forged-proof"), EnrollmentFailure.ProviderRejected, default), "access_denied");
Check(f.Repository.Receipts.Count == 1, "forged finalizer cannot record any terminal result");
var failed = await f.Journal.CompleteFailureAsync(start.Handle!, EnrollmentFailure.ProviderUnavailable, default);
var failureReplay = await f.Journal.CompleteFailureAsync(start.Handle!, EnrollmentFailure.InternalFailure, default);
Check(failed == failureReplay && failed.Code == "face_provider_unavailable", "terminal failure stays immutable on late alternative failure");
var failureWon = await f.Repository.RunAsync(ct => f.Journal.CompleteSuccessAsync(start.Handle!, f.Response(start), null, ct), default);
Check(failureWon == failed && f.Repository.Receipts.Count == 2, "failed winner prevents conflicting success terminal");
Check((await f.Start("attempt-key-001")).Terminal == failed, "same-key caller gets frozen failure outcome");
var winner = await f.Start("attempt-key-002");
var success = await f.Repository.RunAsync(ct => f.Journal.CompleteSuccessAsync(winner.Handle!, f.Response(winner), null, ct), default);
var lateFailure = await f.Journal.CompleteFailureAsync(winner.Handle!, EnrollmentFailure.FinalizationDenied, default);
Check(lateFailure == success && success.Status == 200, "late failed completion never overwrites an already committed success");
var race = await f.Start("attempt-key-003");
await Task.WhenAll(f.Journal.CompleteFailureAsync(race.Handle!, EnrollmentFailure.ProviderRejected, default),
    f.Journal.CompleteFailureAsync(race.Handle!, EnrollmentFailure.ProviderUnavailable, default));
Check(f.Repository.Receipts.Count(x => x.Operation == FaceEnrollmentJournal.TerminalOperation && x.Key == race.Start.ReceiptId.ToString("N")) == 1,
    "competing failure completions share one terminal receipt");
Check(f.Repository.Audits.Count(x => x.TargetId == race.Start.ReceiptId.ToString()) == 2, "one start and one result audit only");
var revoked = await f.Start("attempt-key-004");
// The process still owns the proof issued by the previously authenticated start; no live grant is invented here.
var denied = await f.Journal.CompleteFailureAsync(revoked.Handle!, EnrollmentFailure.FinalizationDenied, default);
Check(denied.Stage == "finalization_denied" && denied.Enrollment is null, "revoked authority can be audited without creating an enrollment");
Check(f.Repository.Audits.Last().ActorUserId == f.Actor.Identity.AccountId.ToString() && f.Repository.Audits.Last().EmployeeScopeId == f.Employee,
    "audit finalizer derives actor and target from persisted start");
var anotherActor = f.Actor with { Identity = f.Actor.Identity with { AccountId = Guid.NewGuid() } };
var anotherStart = await f.Repository.RunAsync(ct => f.Journal.BeginAsync(anotherActor, f.Employee, f.Device, "attempt-key-001", new string('B', 64),
    new(true, "synthetic", "1", "never retained"), [new(EmployeeId: f.Employee)], new("127.0.0.1", "another-authorized-actor"), ct), default);
Check(anotherStart.Start.ReceiptId != start.Start.ReceiptId, "same client key and target from two actors gets different provider attempt identities");
await f.Journal.CompleteFailureAsync(anotherStart.Handle!, EnrollmentFailure.ProviderRejected, default);
var contested = await f.Start("attempt-key-race");
var entered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
var release = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
var successfulWriter = f.Repository.RunAsync(async ct =>
{
    entered.SetResult(true); await release.Task;
    return await f.Journal.CompleteSuccessAsync(contested.Handle!, f.Response(contested), null, ct);
}, default);
await entered.Task;
var waitingFailure = f.Journal.CompleteFailureAsync(contested.Handle!, EnrollmentFailure.InternalFailure, default);
release.SetResult(true);
var raceResults = await Task.WhenAll(successfulWriter, waitingFailure);
Check(raceResults[0] == raceResults[1] && raceResults[0].Status == 200, "concurrent success and failure arbitrate one immutable winning terminal under the same lock");
var expired = await f.Start("attempt-key-005"); f.Clock.Now = f.Clock.Now.AddHours(25);
await Deny(() => f.Start("attempt-key-005"), "idempotency_key_expired");
Check((await f.Journal.CompleteFailureAsync(expired.Handle!, EnrollmentFailure.InternalFailure, default)).Enrollment is null,
    "late failure observability does not treat receipt expiry as audit deletion");
Check(f.Repository.Receipts.Count == 14 && f.Repository.Audits.Count == 14, "journal only contains paired metadata start and terminal records");
Console.WriteLine($"Face enrollment journal checks passed: {checks}. Synthetic serialized IO only; no SQL/provider activity.");

public sealed class JournalClock : TimeProvider
{ public DateTimeOffset Now = new(2026, 10, 9, 10, 0, 0, TimeSpan.Zero); public override DateTimeOffset GetUtcNow() => Now; }
public sealed class JournalFiles : IPrivateFileStore
{
    public Dictionary<string, (string Purpose, byte[] Content)> Objects = [];
    public Task<PrivateFileReference> WriteAsync(byte[] content, string purpose, CancellationToken ct)
    {
        var key = Guid.NewGuid().ToString("N"); Objects.Add(key, (purpose, content.ToArray()));
        return Task.FromResult(new PrivateFileReference(key, "1", Convert.ToHexString(SHA256.HashData(content)), "synthetic", content.Length));
    }
    public Task<byte[]> ReadAsync(PrivateFileReference reference, string purpose, CancellationToken ct)
    {
        var value = Objects[reference.StorageKey];
        if (value.Purpose != purpose) throw new Exception("Purpose mismatch");
        return Task.FromResult(value.Content.ToArray());
    }
}
public sealed class JournalRepository : IFaceEnrollmentJournalRepository
{
    public List<IdempotencyReceipt> Receipts = [];
    public List<AuditEvent> Audits = [];
    private readonly SemaphoreSlim mutex = new(1);
    private bool active;
    public void RequireTransaction() { if (!active) throw new Exception("Missing transaction"); }
    public Task<IdempotencyReceipt?> FindAsync(string actor, string operation, string key, CancellationToken ct) =>
        Task.FromResult(Receipts.SingleOrDefault(x => x.ActorUserId == actor && x.Operation == operation && x.Key == key));
    public Task<IdempotencyReceipt?> StartAsync(Guid id, CancellationToken ct) => Task.FromResult(Receipts.SingleOrDefault(x => x.Id == id));
    public void Add(IdempotencyReceipt row) { RequireTransaction(); Receipts.Add(row); }
    public void Add(AuditEvent row) { RequireTransaction(); Audits.Add(row); }
    public Task SaveChangesAsync(CancellationToken ct) { RequireTransaction(); return Task.CompletedTask; }
    public Task<T> InAuditTransactionAsync<T>(Func<CancellationToken, Task<T>> action, CancellationToken ct) => RunAsync(action, ct);
    public async Task<T> RunAsync<T>(Func<CancellationToken, Task<T>> action, CancellationToken ct)
    { await mutex.WaitAsync(ct); active = true; try { return await action(ct); } finally { active = false; mutex.Release(); } }
}
public sealed class JournalFixture
{
    public JournalClock Clock = new(); public JournalFiles Files = new(); public JournalRepository Repository = new();
    public AttendanceActor Actor; public Guid Employee = Guid.NewGuid(), Device = Guid.NewGuid(); public FaceEnrollmentJournal Journal;
    public JournalFixture()
    {
        Actor = new(new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 1, SessionClient.Web, true), "SyntheticOperator", Clock.Now, null);
        Journal = new(Repository, Files, Clock);
    }
    public Task<EnrollmentExecution> Start(string key, string? hash = null) => Repository.RunAsync(ct => Journal.BeginAsync(Actor, Employee, Device,
        key, hash ?? new string('A', 64), new(true, "synthetic", "1", "untrusted reason never retained"), [new(EmployeeId: Employee)],
        new("127.0.0.1", "synthetic-audit"), ct), default);
    public FaceEnrollmentResponse Response(EnrollmentExecution start) => new(Guid.NewGuid(), Employee, "synthetic", "1", Clock.Now, true, null, null, "AAAAAAAAAAE=");
}
