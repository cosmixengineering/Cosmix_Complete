#:project ../../src/Cosmix.Infrastructure/Cosmix.Infrastructure.csproj
#:property PublishAot=false
#:property RestorePackagesWithLockFile=false
using System.Security.Cryptography;
using System.Text.Json;
using Cosmix.Application.Attendance.Common;
using Cosmix.Application.Attendance.FaceVerification;
using Cosmix.Application.Authentication;
using Cosmix.Application.Authentication.Access;
using Cosmix.Application.Common.Files;
using Cosmix.Domain.Attendance.FaceVerification;
using Cosmix.Domain.Authentication;
using Cosmix.Domain.Common.Audit;
using Cosmix.Domain.Common.Idempotency;
using System.Reflection;

// Real Application workflow and use gate; serialized synthetic persistence, no SQL or provider.
var checks = 0;
void Check(bool value, string name) { if (!value) throw new Exception(name); checks++; }
async Task Reject(Func<Task> action, string name)
{
    try { await action(); }
    catch (AuthenticationFailure) { checks++; return; }
    catch (InvalidOperationException) { checks++; return; }
    catch (JsonException) { checks++; return; }
    throw new Exception(name);
}
var clock = new RetirementClock();
var repository = new RetirementRepository();
var evidence = new RetirementFiles();
var service = new FaceRetirementService(repository, evidence, clock);
var vault = new RetirementVault();
var journal = new RetirementJournal();
var store = new RetentionControlledFaceTemplateStore(vault, journal, new()
{
    Enabled = true, AuthorityConfirmed = true, ApprovalReference = "synthetic-use-only",
    CaptureUseLifetimeSeconds = 30, EnrollmentUseLifetimeSeconds = 60
}, clock);
var physical = new RetirementPhysicalDisposal();
var pendingDisposal = new FaceDisposalRunner(new(), new PendingFaceDisposalPolicy(), physical, clock);
// The concrete gate implements the retirement interface, without widening IFaceTemplateStore.
var retirement = (IFaceEvidenceRetirement)(object)store;
var processor = new FaceRetirementProcessor(repository, service, retirement, pendingDisposal, clock);
var employee = Guid.NewGuid();
var actor = new AttendanceActor(new(Guid.NewGuid(), employee, Guid.NewGuid(), 1, SessionClient.Web, true),
    "SyntheticOperator", clock.GetUtcNow(), null);
var context = new AuthRequestContext("127.0.0.1", "synthetic-b05");
var reference = await store.WriteAsync([1, 3, 5], $"attendance:face:template:{employee:N}", default);
var enrollment = new FaceEnrollment(employee, JsonSerializer.Serialize(reference), reference.KeyReference,
    "synthetic", "1", actor.Identity.AccountId.ToString(), Guid.NewGuid().ToString(), "Synthetic", clock.GetUtcNow(), "synthetic");
enrollment.Revoke(actor.Identity.AccountId.ToString(), "Synthetic revocation", clock.GetUtcNow());

await Reject(() => service.StageEnrollmentAsync(actor, enrollment, null, context, default), "staging requires outer transaction");
await Reject(() => repository.TransactionAsync(async ct =>
{
    await service.StageEnrollmentAsync(actor, enrollment, null, context, ct);
    throw new InvalidOperationException("Synthetic business rollback");
}, default), "rollback simulation");
Check(repository.Committed.Count == 0, "rollback leaves no committed retirement intent");
var bytes = await store.ReadAsync(reference, $"attendance:face:template:{employee:N}", default);
Check(bytes.SequenceEqual(new byte[] { 1, 3, 5 }), "rollback does not close still-valid storage use");
CryptographicOperations.ZeroMemory(bytes);
Check(journal.Events.All(x => x.Kind != FaceStorageRetentionEventKind.Retired), "staging has no ciphertext retirement side effect");

await repository.TransactionAsync(ct => service.StageEnrollmentAsync(actor, enrollment, null, context, ct), default);
await repository.TransactionAsync(ct => service.StageEnrollmentAsync(actor, enrollment, null, context, ct), default);
Check(repository.Committed.Count == 1, "exact repeated stage has one durable intent");
var indexed = repository.Committed.Single();
var intent = await service.ReadIntentAsync(indexed, default);
Check(intent.Reference == reference && intent.EmployeeId == employee && intent.SubjectId == enrollment.Id,
    "intent retains complete exact reference and employee scope");
await Reject(() => repository.TransactionAsync(ct => service.StageEnrollmentAsync(
    actor, enrollment, Guid.NewGuid(), context, ct), default), "competing replacement cannot overwrite intent");
var pending = await processor.RunAsync(null, 20, default);
Check(pending.Pending == 1 && journal.Events.All(x => x.Kind != FaceStorageRetentionEventKind.Retired),
    "uncommitted or absent source proof cannot retire");
repository.Proofs[enrollment.Id] = FaceRetirementProof.Committed;
var processed = await processor.RunAsync(null, 20, default);
Check(processed.UseClosed == 1 && processed.Pending == 1 && physical.Calls == 0,
    "committed revoke closes use and leaves physical disposal explicitly pending");
await Reject(() => store.ReadAsync(reference, intent.Purpose, default), "retired evidence cannot release bytes");
Check(vault.Objects.Count == 1, "retirement preserves ciphertext and its key reference");
var outcome = await service.ReadOutcomeAsync(indexed, default);
Check(outcome is { State: FaceRetirementState.PendingPolicy, UseClosed: true }, "pending policy is durable and observable");
var oldCount = repository.Committed.Count;
var restarted = new FaceRetirementProcessor(repository, new(repository, evidence, clock), retirement, pendingDisposal, clock);
await restarted.RunAsync(null, 20, default);
Check(repository.Committed.Count == oldCount && journal.Events.Count(x => x.Kind == FaceStorageRetentionEventKind.Retired) == 1,
    "restart and repeated processing do not duplicate retirement or unchanged status");

var captureId = Guid.NewGuid(); var attempt = Guid.NewGuid(); var device = Guid.NewGuid();
var captureActor = actor with { Identity = actor.Identity with { Client = SessionClient.Mobile }, DeviceId = device };
var captureReference = await store.WriteAsync([7, 9], FaceCaptureService.Purpose(employee, captureId), default);
var capture = new FaceCaptureEnvelope(1, captureId, new(actor.Identity.AccountId, actor.Identity.SessionId,
    employee, device, attempt, new string('A', 64)), captureReference, "image/png", clock.GetUtcNow(),
    clock.GetUtcNow().AddSeconds(20), [new(EmployeeId: employee)]);
await repository.TransactionAsync(ct => service.StageCaptureAsync(captureActor, capture,
    $"verification:{attempt:N}", new string('B', 64), context, ct), default);
await Reject(() => repository.TransactionAsync(ct => service.StageCaptureAsync(captureActor, capture,
    $"verification:{Guid.NewGuid():N}", new string('B', 64), context, ct), default), "capture operation identity substitution rejected");
await Reject(() => repository.TransactionAsync(ct => service.StageCaptureAsync(captureActor,
    capture with { Binding = capture.Binding with { SessionId = Guid.NewGuid() } }, $"verification:{attempt:N}",
    new string('B', 64), context, ct), default), "capture session substitution rejected");
clock.Now = clock.Now.AddMinutes(2);
await processor.RunAsync(null, 20, default);
Check(!journal.Events.Any(x => x.Reference == captureReference && x.Kind == FaceStorageRetentionEventKind.Retired),
    "capture use/token expiry is not committed completion authority");
repository.Proofs[captureId] = FaceRetirementProof.Rejected;
var rejected = await processor.RunAsync(null, 20, default);
Check(rejected.Failed == 1 && physical.Calls == 0, "wrong committed scope blocks unrelated capture retirement");
repository.Proofs[captureId] = FaceRetirementProof.Committed;
journal.FailNextAppend = true;
var failed = await processor.RunAsync(null, 20, default);
Check(failed.Failed == 1, "journal IO failure remains retryable with durable status");
Check(!journal.Events.Any(x => x.Reference == captureReference && x.Kind == FaceStorageRetentionEventKind.Retired),
    "failed retirement cannot falsely acknowledge closed use");
await restarted.RunAsync(null, 20, default);
Check(journal.Events.Any(x => x.Reference == captureReference && x.Kind == FaceStorageRetentionEventKind.Retired),
    "restart recovers a failed committed capture retirement");

var crashId = Guid.NewGuid();
var crashReference = await store.WriteAsync([11], FaceCaptureService.Purpose(employee, crashId), default);
var crashCapture = capture with { CaptureId = crashId, Image = crashReference,
    IssuedAtUtc = clock.Now, ExpiresAtUtc = clock.Now.AddSeconds(20) };
await repository.TransactionAsync(ct => service.StageCaptureAsync(captureActor, crashCapture,
    $"verification:{attempt:N}", new string('B', 64), context, ct), default);
repository.Proofs[crashId] = FaceRetirementProof.Committed;
repository.FailOutcomeSave = true;
try { await processor.RunAsync(null, 20, default); } catch (InvalidOperationException) { }
repository.FailOutcomeSave = false;
Check(journal.Events.Count(x => x.Reference == crashReference && x.Kind == FaceStorageRetentionEventKind.Retired) == 1,
    "crash after storage retirement leaves durable object disposition");
await restarted.RunAsync(null, 20, default);
Check(journal.Events.Count(x => x.Reference == crashReference && x.Kind == FaceStorageRetentionEventKind.Retired) == 1,
    "uncertain outcome replay reconciles exact object without a second retirement");
Check((await service.ReadOutcomeAsync(repository.Committed.Single(x => x.TargetId == crashId.ToString()), default))?.UseClosed == true,
    "restart persists acknowledgement after uncertain outcome commit");

var policy = new RetirementPolicy();
var enabled = new FaceDisposalRunner(new() { Enabled = true }, policy, physical, clock);
policy.Decision = new(FaceRetirementState.Disposed, new(indexed.Id, intent, clock.Now,
    "synthetic-retention", "synthetic-hold", "synthetic-backup", "synthetic-key"));
Check(await pendingDisposal.RunAsync(indexed.Id, intent, default) == FaceRetirementState.PendingPolicy && physical.Calls == 0,
    "disabled disposal never evaluates an approval into deletion");
policy.Decision = policy.Decision with { Authorization = policy.Decision.Authorization! with { BackupApproval = "" } };
await Reject(() => enabled.RunAsync(indexed.Id, intent, default), "backup authority cannot be omitted");
policy.Decision = policy.Decision with { Authorization = policy.Decision.Authorization! with { BackupApproval = "synthetic-backup",
    Intent = intent with { Reference = captureReference } } };
await Reject(() => enabled.RunAsync(indexed.Id, intent, default), "disposal grant cannot substitute unrelated object");
policy.Decision = policy.Decision with { Authorization = policy.Decision.Authorization! with { Intent = intent, NotBeforeUtc = clock.Now.AddSeconds(1) } };
Check(await enabled.RunAsync(indexed.Id, intent, default) == FaceRetirementState.PendingBoundary && physical.Calls == 0,
    "approved future disposal boundary is enforced independently of use expiry");
policy.Decision = new(FaceRetirementState.PendingHold);
Check(await enabled.RunAsync(indexed.Id, intent, default) == FaceRetirementState.PendingHold && physical.Calls == 0,
    "hold keeps exact object pending");
policy.Decision = new(FaceRetirementState.Disposed, new(indexed.Id, intent, clock.Now,
    "synthetic-retention", "synthetic-hold", "synthetic-backup", "synthetic-key"));
physical.IsConfigured = false;
Check(await enabled.RunAsync(indexed.Id, intent, default) == FaceRetirementState.PendingDisposalAdapter,
    "authority without approved storage adapter remains pending");
physical.IsConfigured = true;
Check(await enabled.RunAsync(indexed.Id, intent, default) == FaceRetirementState.Disposed && physical.Calls == 1,
    "only exact approved synthetic disposal reaches the nondeleting fixture");

await Reject(() => repository.TransactionAsync(async ct => { await processor.RunAsync(null, 20, ct); }, default),
    "worker cannot run inside business transaction");
using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
try { await restarted.RunAsync(null, 20, cancelled.Token); throw new Exception("Cancellation ignored"); }
catch (OperationCanceledException) { checks++; }

Check(FaceRetirementSourceRules.Enrollment(intent, enrollment, null, false) == FaceRetirementProof.Committed,
    "actual source rules accept exact committed revoked enrollment");
Check(FaceRetirementSourceRules.Enrollment(intent, enrollment, null, true) == FaceRetirementProof.Rejected,
    "object referenced by an active enrollment cannot retire");
Check(FaceRetirementSourceRules.Enrollment(intent with { Reference = captureReference }, enrollment, null, false) == FaceRetirementProof.Rejected,
    "source reference substitution rejected independently of persisted intent");
var crossEmployee = Guid.NewGuid();
Check(FaceRetirementSourceRules.Enrollment(intent with { EmployeeId = crossEmployee, Purpose = $"attendance:face:template:{crossEmployee:N}" },
    enrollment, null, false) != FaceRetirementProof.Committed, "cross employee source cannot retire");
var successor = new FaceEnrollment(employee, JsonSerializer.Serialize(captureReference), captureReference.KeyReference,
    "synthetic", "1", actor.Identity.AccountId.ToString(), device.ToString(), "Synthetic replacement", clock.Now, "synthetic", enrollment.Id);
var replacedIntent = intent with { Reason = FaceStorageRetentionReason.EnrollmentReplaced, ReplacementEnrollmentId = successor.Id };
Check(FaceRetirementSourceRules.Enrollment(replacedIntent, enrollment, successor, false) == FaceRetirementProof.Committed &&
    FaceRetirementSourceRules.Enrollment(replacedIntent, enrollment, null, false) == FaceRetirementProof.Rejected,
    "replacement proof names the exact committed successor");

var captureAudit = repository.Committed.Single(x => x.EventType == FaceRetirementService.IntentEvent && x.TargetId == captureId.ToString());
var captureIntent = await service.ReadIntentAsync(captureAudit, default);
IdempotencyReceipt ClaimReceipt(FaceRetirementIntent value)
{
    var envelope = value.Capture!; var b = envelope.Binding; var self = b.AttemptId.HasValue;
    var digest = Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new
    {
        permission = self ? "attendance.face.self" : "attendance.face.manage", employeeId = (Guid?)b.EmployeeId,
        DeviceId = self ? (Guid?)b.DeviceId : null,
        payload = new { operationKey = value.OperationKey, envelope.CaptureId, TokenDigest = value.TokenSha256, b.SessionId },
        reason = "Server claim of bound biometric evidence"
    })));
    var receipt = new IdempotencyReceipt(b.AccountId.ToString(), "attendance.face.capture.claim", $"capture:{envelope.CaptureId:N}",
        digest, envelope.IssuedAtUtc, envelope.ExpiresAtUtc);
    receipt.Complete("attendance-operation-v1", JsonSerializer.Serialize(reference), envelope.IssuedAtUtc);
    return receipt;
}
var committedClaim = ClaimReceipt(captureIntent);
Check(FaceRetirementSourceRules.Claim(captureIntent, committedClaim, captureIntent.Capture!), "actual committed claim digest matches all server bindings");
Check(!FaceRetirementSourceRules.Claim(captureIntent with { TokenSha256 = new string('C', 64) }, committedClaim, captureIntent.Capture!),
    "a different capture token digest cannot borrow the receipt");
Check(!FaceRetirementSourceRules.Claim(captureIntent, committedClaim, captureIntent.Capture! with { Image = crashReference }),
    "a different private object cannot borrow the capture receipt");
var account = (UserAccount)Activator.CreateInstance(typeof(UserAccount), true)!;
typeof(UserAccount).GetProperty("Id")!.SetValue(account, actor.Identity.AccountId);
typeof(UserAccount).GetProperty("EmployeeId")!.SetValue(account, employee);
typeof(UserAccount).GetProperty("Status")!.SetValue(account, AccountStatus.Active);
var realDevice = new RegisteredDevice(account.Id, "synthetic", "android", "Synthetic", capture.IssuedAtUtc.AddSeconds(-1));
realDevice.MarkVerified(capture.IssuedAtUtc.AddSeconds(-1));
var activeEnrollment = new FaceEnrollment(employee, JsonSerializer.Serialize(reference), reference.KeyReference,
    "synthetic", "1", actor.Identity.AccountId.ToString(), realDevice.Id.ToString(), "Synthetic", capture.IssuedAtUtc, "synthetic");
var verification = new FaceVerificationAttempt(activeEnrollment, account, realDevice, "synthetic-punch-key", new string('A', 64),
    "synthetic", "1", capture.IssuedAtUtc, capture.ExpiresAtUtc, "synthetic");
var boundCapture = captureIntent with { Capture = capture with { Binding = capture.Binding with { DeviceId = realDevice.Id, AttemptId = verification.Id } },
    OperationKey = $"verification:{verification.Id:N}" };
var verificationClaim = ClaimReceipt(boundCapture);
Check(FaceRetirementSourceRules.Verification(boundCapture, verificationClaim, verification) == FaceRetirementProof.Pending,
    "pending challenge has no retirement completion proof even after token expiry");
verification.RecordProviderResult(true, false, false, "FAILED", capture.IssuedAtUtc.AddSeconds(1));
Check(FaceRetirementSourceRules.Verification(boundCapture, verificationClaim, verification) == FaceRetirementProof.Committed,
    "committed failed verification also completes the capture");
Check(FaceRetirementSourceRules.Verification(boundCapture, verificationClaim, null) == FaceRetirementProof.Rejected,
    "unrelated challenge cannot complete capture");

var startRow = new IdempotencyReceipt(actor.Identity.AccountId.ToString(), FaceEnrollmentJournal.StartOperation,
    "synthetic-enroll-key", new string('D', 64), capture.IssuedAtUtc, capture.IssuedAtUtc.AddHours(24));
startRow.Complete("face-enrollment-start-v1", JsonSerializer.Serialize(reference), capture.IssuedAtUtc);
var start = new EnrollmentStart(startRow.Id, actor.Identity.AccountId, actor.RoleCode, employee, device,
    startRow.RequestSha256, new string('E', 64), "synthetic", "1", capture.IssuedAtUtc, "synthetic", actor.Identity.SessionId.ToString(),
    "127.0.0.1", [new(EmployeeId: employee)]);
var enrollmentCapture = captureIntent with { OperationKey = $"enrollment:{startRow.Id:N}",
    Capture = capture with { Binding = capture.Binding with { AttemptId = null, ChallengeSha256 = null } } };
var enrollmentClaim = ClaimReceipt(enrollmentCapture);
Check(FaceRetirementSourceRules.EnrollmentCapture(enrollmentCapture, enrollmentClaim, startRow, start, null, null) == FaceRetirementProof.Pending,
    "unfinished enrollment preserves capture for same operation recovery");
var description = FaceEnrollmentJournal.Describe(EnrollmentFailure.ProviderUnavailable);
var terminal = new EnrollmentTerminal(startRow.Id, employee, device, "synthetic", "1", capture.IssuedAtUtc.AddSeconds(2),
    description.Stage, description.Code, description.Status, null);
var terminalRow = new IdempotencyReceipt(actor.Identity.AccountId.ToString(), FaceEnrollmentJournal.TerminalOperation,
    startRow.Id.ToString("N"), startRow.RequestSha256, terminal.CompletedAtUtc, terminal.CompletedAtUtc.AddSeconds(1));
terminalRow.Complete("face-enrollment-terminal-v1", JsonSerializer.Serialize(reference), terminal.CompletedAtUtc);
Check(FaceRetirementSourceRules.EnrollmentCapture(enrollmentCapture, enrollmentClaim, startRow, start, terminalRow, terminal) == FaceRetirementProof.Committed,
    "committed enrollment failure retires claimed capture even after receipt retry expiry");
Check(FaceRetirementSourceRules.EnrollmentCapture(enrollmentCapture, enrollmentClaim, startRow,
    start with { ActorSessionReference = Guid.NewGuid().ToString() }, terminalRow, terminal) == FaceRetirementProof.Rejected,
    "enrollment terminal cannot cross the original capture session");
Check(FaceRetirementSourceRules.EnrollmentCapture(enrollmentCapture, enrollmentClaim, startRow, start, terminalRow,
    terminal with { EmployeeId = Guid.NewGuid() }) == FaceRetirementProof.Rejected, "enrollment terminal cannot cross employee scope");

var offboarded = new FaceEnrollment(employee, JsonSerializer.Serialize(reference), reference.KeyReference, "synthetic", "1",
    actor.Identity.AccountId.ToString(), device.ToString(), "Synthetic", clock.Now, "synthetic");
var changeId = Guid.NewGuid();
offboarded.Revoke("system:employee-lifecycle", $"employee-lifecycle:{changeId:D}", clock.Now);
var authorityAudit = new AuditEvent("hr.employee.lifecycle.effective", "system:employee-lifecycle", "SYSTEM",
    "EmployeeStatusChange", changeId.ToString(), clock.Now, changeId.ToString("N"), employee);
repository.Revocations.Add(new(offboarded, null, authorityAudit));
Check((await service.RecoverRevocationsAsync(20, default)).Recovered == 1, "A04 committed revoked row recovers without an HR hook or model change");
var recoveredAudit = repository.Committed.Single(x => x.TargetId == offboarded.Id.ToString());
Check(recoveredAudit.ActorUserId == authorityAudit.ActorUserId && recoveredAudit.ActorRoleCode == authorityAudit.ActorRoleCode,
    "offboarding recovery preserves original audited actor and role");
Check((await service.RecoverRevocationsAsync(20, default)).Recovered == 0, "offboarding recovery is idempotent across sweeps");
var serializedIntent = JsonSerializer.Serialize(intent);
await Reject(() => Task.FromResult(FaceRetirementEvidence.Deserialize<FaceRetirementIntent>(System.Text.Encoding.UTF8.GetBytes(
    serializedIntent.Replace("\"Version\":1,", "")))), "missing required intent version rejected");
await Reject(() => Task.FromResult(FaceRetirementEvidence.Deserialize<FaceRetirementIntent>(System.Text.Encoding.UTF8.GetBytes(
    serializedIntent.Replace("\"Version\":1", "\"Version\":1,\"Version\":1")))), "duplicate intent properties rejected");
Check(typeof(FaceRetirementBatch).GetProperty("RecoveryPending") is not null,
    "revoked rows missing their original authority audit are observable as pending");
var missingAuthority = new FaceEnrollment(employee, JsonSerializer.Serialize(reference), reference.KeyReference, "synthetic", "1",
    actor.Identity.AccountId.ToString(), device.ToString(), "Synthetic", clock.Now, "synthetic");
missingAuthority.Revoke(actor.Identity.AccountId.ToString(), "Synthetic legacy source", clock.Now);
repository.Revocations.Add(new(missingAuthority, null));
var unresolved = await service.RecoverRevocationsAsync(1, default);
Check(unresolved.Pending == 1 && unresolved.NextCursor is not null, "missing authority remains explicitly pending with recoverable pagination");
Check((await processor.RunAsync(null, 20, default)).RecoveryPending == 1,
    "missing original audit does not block already-staged retirement work");
Console.WriteLine($"B05 retirement workflow checks passed: {checks}. Synthetic persistence; no SQL/provider/deletion.");

public sealed class RetirementClock : TimeProvider
{ public DateTimeOffset Now = new(2026, 10, 13, 0, 0, 0, TimeSpan.Zero); public override DateTimeOffset GetUtcNow() => Now; }
public sealed class RetirementRepository : IFaceRetirementRepository
{
    public List<AuditEvent> Committed = []; private List<AuditEvent> staged = [];
    public Dictionary<Guid, FaceRetirementProof> Proofs = []; public List<FaceRevocationSource> Revocations = [];
    public bool FailOutcomeSave; private bool transaction;
    public void RequireTransaction() { if (!transaction) throw new InvalidOperationException("Missing transaction"); }
    public void RequireOutsideTransaction() { if (transaction) throw new InvalidOperationException("Worker inside transaction"); }
    public Task<AuditEvent?> FindIntentAsync(string type, Guid id, CancellationToken ct) => Task.FromResult(
        Committed.Concat(staged).SingleOrDefault(x => x.EventType == FaceRetirementService.IntentEvent && x.TargetType == type && x.TargetId == id.ToString()));
    public Task<AuditEvent?> LatestOutcomeAsync(Guid id, CancellationToken ct) => Task.FromResult(Committed.Concat(staged)
        .Where(x => x.TargetType == "FaceRetirementOutcome" && x.TargetId == id.ToString()).OrderByDescending(x => x.OccurredAt).FirstOrDefault());
    public Task<IReadOnlyList<AuditEvent>> IntentsAsync(FaceRetirementCursor? after, int size, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<AuditEvent>>(Committed.Where(x => x.EventType == FaceRetirementService.IntentEvent &&
            (after is null || x.OccurredAt > after.OccurredAtUtc || x.OccurredAt == after.OccurredAtUtc && x.Id.CompareTo(after.Id) > 0))
            .OrderBy(x => x.OccurredAt).ThenBy(x => x.Id).Take(size).ToArray());
    public Task<IReadOnlyList<FaceRevocationSource>> UnstagedRevocationsAsync(FaceRetirementCursor? after, int size, CancellationToken ct) => Task.FromResult<IReadOnlyList<FaceRevocationSource>>(
        Revocations.Where(x => !Committed.Any(a => a.EventType == FaceRetirementService.IntentEvent && a.TargetType == FaceRetirementService.EnrollmentTarget &&
            a.TargetId == x.Enrollment.Id.ToString()) && (after is null || x.Enrollment.RevokedAtUtc > after.OccurredAtUtc ||
                x.Enrollment.RevokedAtUtc == after.OccurredAtUtc && x.Enrollment.Id.CompareTo(after.Id) > 0))
            .OrderBy(x => x.Enrollment.RevokedAtUtc).ThenBy(x => x.Enrollment.Id).Take(size).ToArray());
    public Task<FaceRetirementProof> ValidateCommittedAsync(FaceRetirementIntent intent, CancellationToken ct)
    { RequireOutsideTransaction(); return Task.FromResult(Proofs.GetValueOrDefault(intent.SubjectId)); }
    public void Add(AuditEvent audit) { RequireTransaction(); staged.Add(audit); }
    public Task SaveChangesAsync(CancellationToken ct)
    {
        RequireTransaction(); ct.ThrowIfCancellationRequested();
        if (FailOutcomeSave && staged.Any(x => x.TargetType == "FaceRetirementOutcome")) throw new InvalidOperationException("Synthetic audit outage");
        return Task.CompletedTask;
    }
    public async Task TransactionAsync(Func<CancellationToken, Task> action, CancellationToken ct) =>
        await InAuditTransactionAsync(async token => { await action(token); return true; }, ct);
    public async Task<T> InAuditTransactionAsync<T>(Func<CancellationToken, Task<T>> action, CancellationToken ct)
    {
        if (transaction) throw new InvalidOperationException("Nested transaction");
        transaction = true;
        try { var result = await action(ct); Committed.AddRange(staged); return result; }
        finally { staged = []; transaction = false; }
    }
}
public sealed class RetirementFiles : IPrivateFileStore
{
    public Dictionary<string, (string Purpose, byte[] Bytes)> Objects = [];
    public Task<PrivateFileReference> WriteAsync(byte[] content, string purpose, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested(); var id = Guid.NewGuid().ToString("N") + ".enc";
        Objects.Add(id, (purpose, content.ToArray()));
        return Task.FromResult(new PrivateFileReference(id, Guid.NewGuid().ToString("N"), Convert.ToHexString(SHA256.HashData(content)), "synthetic-key", content.Length));
    }
    public Task<byte[]> ReadAsync(PrivateFileReference reference, string purpose, CancellationToken ct)
    { ct.ThrowIfCancellationRequested(); var value = Objects[reference.StorageKey]; if (value.Purpose != purpose) throw FaceRetirementEvidence.Unavailable(); return Task.FromResult(value.Bytes.ToArray()); }
}
public sealed class RetirementVault : IFaceTemplateStore
{
    private readonly RetirementFiles files = new(); public Dictionary<string, (string Purpose, byte[] Bytes)> Objects => files.Objects;
    public bool IsConfigured => true;
    public Task<PrivateFileReference> WriteAsync(byte[] template, string purpose, CancellationToken ct) => files.WriteAsync(template, purpose, ct);
    public Task<byte[]> ReadAsync(PrivateFileReference reference, string purpose, CancellationToken ct) => files.ReadAsync(reference, purpose, ct);
}
public sealed class RetirementJournal : IFaceStorageRetentionJournal
{
    public List<FaceStorageRetentionEvent> Events = []; public bool FailNextAppend;
    private readonly SemaphoreSlim mutex = new(1); public bool IsConfigured => true;
    public async Task<T> WithObjectLockAsync<T>(PrivateFileReference reference, Func<CancellationToken, Task<T>> action, CancellationToken ct)
    { await mutex.WaitAsync(ct); try { return await action(ct); } finally { mutex.Release(); } }
    public Task<IReadOnlyList<FaceStorageRetentionEvent>> ReadAsync(PrivateFileReference reference, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<FaceStorageRetentionEvent>>(Events.Where(x => x.Reference == reference).ToArray());
    public Task AppendAsync(FaceStorageRetentionEvent value, CancellationToken ct)
    { if (FailNextAppend) { FailNextAppend = false; throw new IOException("Synthetic journal outage"); } Events.Add(value); return Task.CompletedTask; }
}
public sealed class RetirementPolicy : IFaceDisposalPolicy
{
    public FaceDisposalDecision Decision = new(FaceRetirementState.PendingPolicy);
    public Task<FaceDisposalDecision> EvaluateAsync(Guid intentId, FaceRetirementIntent intent, DateTimeOffset now, CancellationToken ct) => Task.FromResult(Decision);
}
public sealed class RetirementPhysicalDisposal : IFacePhysicalDisposal
{
    public bool IsConfigured { get; set; } = true; public int Calls;
    public Task DisposeAsync(FaceDisposalAuthorization authorization, CancellationToken ct) { Calls++; return Task.CompletedTask; }
}
