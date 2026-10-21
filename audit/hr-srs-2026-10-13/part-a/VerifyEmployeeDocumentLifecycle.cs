using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using Cosmix.Application.Authentication;
using Cosmix.Application.Authentication.Access;
using Cosmix.Application.Common.Files;
using Cosmix.Application.HR.Employees;
using Cosmix.Domain.Authentication;
using Cosmix.Domain.Common.Audit;
using Cosmix.Domain.Common.Idempotency;
using Cosmix.Domain.HR.EmployeeDocuments;
using Cosmix.Domain.HR.Employees;

var passed = 0; var failed = 0;
async Task Check(string name, Func<Task> test)
{
    try { await test(); passed++; Console.WriteLine("PASS " + name); }
    catch (Exception error) { failed++; Console.WriteLine("FAIL " + name + ": " + error.Message); }
}
void Assert(bool value, string message) { if (!value) throw new Exception(message); }
async Task Reject(Func<Task> action, int status, string? code = null)
{
    try { await action(); }
    catch (AuthenticationFailure e) when (e.StatusCode == status && (code is null || e.Code == code)) { return; }
    throw new Exception($"Expected rejection {status}/{code}");
}
var context = new AuthRequestContext("127.0.0.1", "a02-synthetic-only");

await Check("valid download audits both authorization and delivered document", async () =>
{
    var f = new Fixture(); var result = await f.Service.DownloadAsync(f.Employee.Id, f.Document.Id, false, false, context, default);
    Assert(result.Content.SequenceEqual(f.Bytes) && f.ReadCount == 1 && f.Audits.Count == 2, "Download/audit evidence missing");
});
await Check("revoked grant during file I/O denies and clears plaintext", async () =>
{
    var f = new Fixture { DuringRead = x => x.Allowed = false };
    await Reject(() => f.Service.DownloadAsync(f.Employee.Id, f.Document.Id, false, false, context, default), 403);
    Assert(f.LastRead!.All(b => b == 0), "Denied plaintext was retained");
});
await Check("document withdrawal during file I/O denies and clears plaintext", async () =>
{
    var f = new Fixture { DuringRead = x => x.Document.ChangeLifecycle(EmployeeDocumentLifecycleStatus.Revoked, "reviewer", "Withdrawal", x.Now) };
    await Reject(() => f.Service.DownloadAsync(f.Employee.Id, f.Document.Id, false, false, context, default), 409);
    Assert(f.LastRead!.All(b => b == 0), "Withdrawn plaintext was retained");
});
await Check("explicit revoke requires its dedicated permission", async () =>
{
    var f = new Fixture { MissingPermission = EmployeeDocumentPermissions.Revoke };
    await Reject(() => f.Withdraw(EmployeeDocumentLifecycleStatus.Revoked, f.Action(), "deny-revoke"), 403);
    Assert(f.Document.IsAvailable && f.History.Count == 0, "Unauthorized state changed");
});
await Check("blank withdrawal reason rejects before mutation", async () =>
{
    var f = new Fixture(); await Reject(() => f.Withdraw(EmployeeDocumentLifecycleStatus.Revoked, f.Action() with { Reason = " " }, "blank-reason"), 400);
    Assert(f.Document.IsAvailable && f.History.Count == 0, "Blank reason changed evidence");
});
await Check("stale document version rejects even with current employee version", async () =>
{
    var f = new Fixture(); await Reject(() => f.Withdraw(EmployeeDocumentLifecycleStatus.Expired,
        f.Action() with { ExpectedDocumentVersion = Convert.ToBase64String(new byte[8]) }, "stale-version"), 409, "employee_document_version_conflict");
    Assert(f.History.Count == 0, "Stale document gained history");
});
await Check("revoke retains object and exact reason with replay-safe history", async () =>
{
    var f = new Fixture(); var input = f.Action(); var key = f.Document.StorageKey;
    var first = await f.Withdraw(EmployeeDocumentLifecycleStatus.Revoked, input, "revoke-once");
    var replay = await f.Withdraw(EmployeeDocumentLifecycleStatus.Revoked, input, "revoke-once");
    Assert(first == replay && f.History.Count == 1 && f.History[0].Action == EmployeeDocumentHistoryAction.Revoked &&
        f.History[0].Reason == input.Reason && f.Document.StorageKey == key && !f.Document.IsAvailable, "Revoke replay/history failed");
    f.Allowed = false; await Reject(() => f.Withdraw(EmployeeDocumentLifecycleStatus.Revoked, input, "revoke-once"), 403);
});
await Check("reused withdrawal key with changed reason conflicts", async () =>
{
    var f = new Fixture(); var input = f.Action(); await f.Withdraw(EmployeeDocumentLifecycleStatus.Expired, input, "withdraw-key");
    await Reject(() => f.Withdraw(EmployeeDocumentLifecycleStatus.Expired, input with { Reason = "Changed reason" }, "withdraw-key"), 409, "idempotency_key_reused");
    Assert(f.History.Count == 1, "Conflicting replay appended history");
});
await Check("medical privacy mismatch cannot withdraw a standard document", async () =>
{
    var f = new Fixture(); await Reject(() => f.Service.ChangeLifecycleAsync(f.Employee.Id, f.Document.Id, true,
        EmployeeDocumentLifecycleStatus.Revoked, f.Action(), "privacy-deny", context, default), 404);
});
await Check("rescan cancellation leaves committed unavailable attempt", async () =>
{
    var f = new Fixture { Scan = _ => throw new OperationCanceledException() }; var input = f.Action();
    try { await f.Service.RescanAsync(f.Employee.Id, f.Document.Id, false, input, "retry-scan-key", context, default); throw new Exception("Canceled scan succeeded"); }
    catch (OperationCanceledException) { }
    Assert(f.Document.IsScanInProgress && !f.Document.IsAvailable && f.History.Count == 1, "Interrupted scan remained available");
    f.Scan = _ => new(DocumentScanStatus.Clean, "synthetic-scanner/v2");
    await f.Service.RescanAsync(f.Employee.Id, f.Document.Id, false, input, "retry-scan-key", context, default);
    Assert(!f.Document.IsScanInProgress && !f.Document.IsAvailable && f.History.Count == 2, "Retry duplicated start or reused verification");
    await f.Service.VerifyAsync(f.Employee.Id, f.Document.Id, false,
        new(DocumentVerificationStatus.Verified, f.EmployeeVersion, f.DocumentVersion, "Reviewed rescan"), "verify-new-scan", context, default);
    Assert(f.Document.IsAvailable && f.History.Count == 3, "New manual verification failed");
});
await Check("withdrawal during scanner I/O blocks completion", async () =>
{
    var f = new Fixture(); f.Scan = _ => { f.Document.ChangeLifecycle(EmployeeDocumentLifecycleStatus.Revoked, "reviewer", "Withdrawal", f.Now); return new(DocumentScanStatus.Clean, "synthetic/v2"); };
    await Reject(() => f.Service.RescanAsync(f.Employee.Id, f.Document.Id, false, f.Action(), "race-scan-key", context, default), 409);
    Assert(!f.Document.IsAvailable, "Scanner completion restored revoked evidence");
});
await Check("unavailable scanner persists failed state without clean evidence", async () =>
{
    var f = new Fixture { Scan = _ => throw new IOException("Synthetic unavailable scanner") };
    await f.Service.RescanAsync(f.Employee.Id, f.Document.Id, false, f.Action(), "failed-scan-key", context, default);
    Assert(f.Document.ScanStatus == DocumentScanStatus.Failed && !f.Document.IsAvailable && !f.Document.IsScanInProgress, "Scanner error was not fail-closed");
});
await Check("pending scanner result is treated as failed", async () =>
{
    var f = new Fixture { Scan = _ => new(DocumentScanStatus.Pending, "synthetic/v2") };
    await f.Service.RescanAsync(f.Employee.Id, f.Document.Id, false, f.Action(), "invalid-scan-key", context, default);
    Assert(f.Document.ScanStatus == DocumentScanStatus.Failed && !f.Document.IsAvailable, "Invalid scanner response enabled evidence");
});
await Check("malware result cannot be verified or reviewed", async () =>
{
    var f = new Fixture { Scan = _ => new(DocumentScanStatus.Rejected, "synthetic/v2") };
    await f.Service.RescanAsync(f.Employee.Id, f.Document.Id, false, f.Action(), "malware-scan-key", context, default);
    await Reject(() => f.Service.VerifyAsync(f.Employee.Id, f.Document.Id, false,
        new(DocumentVerificationStatus.Verified, f.EmployeeVersion, f.DocumentVersion, null), "malware-verify", context, default), 409);
    await Reject(() => f.Service.DownloadAsync(f.Employee.Id, f.Document.Id, false, true, context, default), 409);
});
await Check("scanner cannot mutate stored upload bytes or digest", async () =>
{
    var f = new Fixture { Scan = bytes => { bytes[0] = 0; return new(DocumentScanStatus.Clean, "synthetic/v2"); } };
    await f.Service.UploadAsync(f.Employee.Id, EmployeeDocumentType.Identity, false, "identity.pdf", "application/pdf", f.Bytes,
        null, null, f.EmployeeVersion, "safe-upload-key", context, default);
    var uploaded = f.AddedDocument!;
    Assert(uploaded.Sha256 == Convert.ToHexString(SHA256.HashData(f.Bytes)) && f.ObjectBytes![0] == (byte)'%', "Scanner mutated immutable private upload");
});
await Check("invalid signature and oversized upload never reach scanner or storage", async () =>
{
    var f = new Fixture();
    await Reject(() => f.Service.UploadAsync(f.Employee.Id, EmployeeDocumentType.Identity, false, "identity.pdf", "application/pdf",
        "invalid"u8.ToArray(), null, null, f.EmployeeVersion, "invalid-upload", context, default), 400);
    await Reject(() => f.Service.UploadAsync(f.Employee.Id, EmployeeDocumentType.Identity, false, "identity.pdf", "application/pdf",
        new byte[EmployeeDocumentService.MaximumUploadBytes + 1], null, null, f.EmployeeVersion, "large-upload-key", context, default), 400);
    Assert(f.ScanCount == 0 && f.WriteCount == 0 && f.AddedDocument is null, "Rejected upload performed I/O");
});
await Check("expired document is denied even though its scan and manual verification are clean", async () =>
{
    var f = new Fixture(expiry: new DateOnly(2026, 10, 12));
    await Reject(() => f.Service.DownloadAsync(f.Employee.Id, f.Document.Id, false, false, context, default), 409);
    Assert(f.ReadCount == 0, "Expired file was read");
});
await Check("completeness requires selected current evidence and treats future expiry as reminder", () =>
{
    var f = new Fixture(expiry: new DateOnly(2026, 10, 20));
    var photo = f.Extra(EmployeeDocumentType.ProfilePhoto);
    var today = new DateOnly(2026, 10, 13);
    var complete = EmployeeDocumentCompleteness.Evaluate(f.Employee.Id, [f.Document, photo], [f.Document.Id, photo.Id], [], today, 14);
    Assert(complete.ActivationBlockers.Count == 0 && complete.ExpiringDocuments.Count == 1, "Expiring valid evidence incorrectly blocked activation");
    var missing = EmployeeDocumentCompleteness.Evaluate(f.Employee.Id, [f.Document, photo], [photo.Id], [], today, 14);
    Assert(missing.RequiredDocuments.Any(d => d.Type == EmployeeDocumentType.Identity && d.IsMissing), "Unselected upload satisfied identity requirement");
    f.Document.ChangeLifecycle(EmployeeDocumentLifecycleStatus.Revoked, "reviewer", "Withdrawal", f.Now);
    var revoked = EmployeeDocumentCompleteness.Evaluate(f.Employee.Id, [f.Document, photo], [f.Document.Id, photo.Id], [], today, 14);
    Assert(revoked.ActivationBlockers.Count > 0 && revoked.UnavailableSelectedDocumentIds.Contains(f.Document.Id), "Revoked evidence satisfied activation");
    return Task.CompletedTask;
});
await Check("certification expiry blocks activation independently from attachment expiry", () =>
{
    var f = new Fixture(); var photo = f.Extra(EmployeeDocumentType.ProfilePhoto);
    var cert = new EmployeeCertificationRequirement("certifications[0]", "License", new DateOnly(2025, 1, 1),
        new DateOnly(2026, 10, 12), f.Document.Id, true);
    var state = EmployeeDocumentCompleteness.Evaluate(f.Employee.Id, [f.Document, photo], [f.Document.Id, photo.Id], [cert], new DateOnly(2026, 10, 13), 14);
    Assert(state.Certifications[0].IsExpired && state.ActivationBlockers.Contains("certifications[0].expired"), "Attachment clean status hid certificate expiry");
    return Task.CompletedTask;
});
await Check("approved renewal proof is distinct from unchanged selected manifest", () =>
{
    var f = new Fixture(); var photo = f.Extra(EmployeeDocumentType.ProfilePhoto); var renewal = f.Extra(EmployeeDocumentType.Certificate);
    var cert = new EmployeeCertificationRequirement("certifications.current", "License", new DateOnly(2026, 10, 1),
        new DateOnly(2027, 10, 1), renewal.Id, true);
    var docs = new[] { f.Document, photo, renewal }; var selected = new[] { f.Document.Id, photo.Id };
    var draft = EmployeeDocumentCompleteness.Evaluate(f.Employee.Id, docs, selected, [cert], new DateOnly(2026, 10, 13), 14);
    var approved = EmployeeDocumentCompleteness.Evaluate(f.Employee.Id, docs, selected, [cert], new DateOnly(2026, 10, 13), 14, true);
    Assert(draft.Certifications[0].IsMissingEvidence && !approved.Certifications[0].IsMissingEvidence && approved.ActivationBlockers.Count == 0,
        "Approved proof was incorrectly gated by original selected manifest");
    f.Document.ChangeLifecycle(EmployeeDocumentLifecycleStatus.Revoked, "reviewer", "Withdrawal", f.Now);
    var withdrawn = EmployeeDocumentCompleteness.Evaluate(f.Employee.Id, docs, selected, [cert], new DateOnly(2026, 10, 13), 14, true);
    Assert(!withdrawn.Certifications[0].IsMissingEvidence && withdrawn.UnavailableSelectedDocumentIds.Contains(f.Document.Id) && withdrawn.ActivationBlockers.Count > 0,
        "Renewal incorrectly suppressed another selected requirement");
    return Task.CompletedTask;
});
Console.WriteLine($"A02 lifecycle/service checks: {passed} passed, {failed} failed");
return failed == 0 ? 0 : 1;

class Fixture
{
    public readonly DateTimeOffset Now = new(2026, 10, 13, 0, 0, 0, TimeSpan.Zero);
    public Employee Employee { get; }
    public EmployeeDocument Document { get; }
    public EmployeeDocument? AddedDocument;
    public EmployeeDocumentService Service { get; }
    public byte[] Bytes = "%PDF-synthetic-only"u8.ToArray();
    public byte[]? LastRead, ObjectBytes;
    public bool Allowed = true;
    public string? MissingPermission;
    public int ReadCount, ScanCount, WriteCount;
    public Action<Fixture>? DuringRead;
    public Func<byte[], FileScanResult> Scan = _ => new(DocumentScanStatus.Clean, "synthetic-scanner/v2");
    public readonly List<EmployeeDocumentHistory> History = [];
    public readonly List<AuditEvent> Audits = [];
    public readonly List<IdempotencyReceipt> Receipts = [];
    public string EmployeeVersion => Convert.ToBase64String(Employee.RowVersion);
    public string DocumentVersion => Convert.ToBase64String(Document.RowVersion);
    public EmployeeDocumentActionRequest Action() => new(EmployeeVersion, DocumentVersion, "Authorized evidence action");
    public Task<EmployeeWriteResult> Withdraw(EmployeeDocumentLifecycleStatus status, EmployeeDocumentActionRequest input, string key) =>
        Service.ChangeLifecycleAsync(Employee.Id, Document.Id, false, status, input, key, new("127.0.0.1", "synthetic-a02"), default);
    public EmployeeDocument Extra(EmployeeDocumentType type) => Create(type, null);
    private EmployeeDocument Create(EmployeeDocumentType type, DateOnly? expiry)
    {
        var reference = new PrivateFileReference("opaque", "version", Convert.ToHexString(SHA256.HashData(Bytes)), "test", Bytes.Length);
        var d = new EmployeeDocument(Employee.Id, type, JsonSerializer.Serialize(reference), "evidence.pdf", "application/pdf", Bytes.Length,
            reference.Sha256, "synthetic-hr", Now.AddDays(-2), expiresOn: expiry);
        typeof(EmployeeDocument).GetProperty("RowVersion")!.SetValue(d, BitConverter.GetBytes(1L));
        d.RecordScan(DocumentScanStatus.Clean, true, "synthetic-scanner/v1", Now.AddDays(-1));
        d.RecordVerification(DocumentVerificationStatus.Verified, "synthetic-reviewer", null, Now);
        return d;
    }
    public Fixture(DateOnly? expiry = null)
    {
        Employee = new Employee(Now.AddDays(-3), "synthetic-hr");
        typeof(Employee).GetProperty("RowVersion")!.SetValue(Employee, BitConverter.GetBytes(1L));
        Document = Create(EmployeeDocumentType.Identity, expiry);
        var actor = new AuthenticatedAccount(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 1, SessionClient.Web, true);
        var all = typeof(EmployeePermissions).GetFields(BindingFlags.Public | BindingFlags.Static)
            .Concat(typeof(EmployeeDocumentPermissions).GetFields(BindingFlags.Public | BindingFlags.Static))
            .Select(p => new PermissionGrant((string)p.GetRawConstantValue()!, true, Guid.NewGuid(), AccessScopeKind.All, null)).ToArray();
        var current = Proxy.Of<ICurrentAuthentication>((_, _) => actor);
        var access = Proxy.Of<IAccountAccessReader>((_, _) => Task.FromResult<IReadOnlyList<PermissionGrant>>(Allowed ? all.Where(g => g.Permission != MissingPermission).ToArray() : []));
        var auth = Proxy.Of<IAccessManagementRepository>((method, args) => method.Name switch
        {
            "InTransactionAsync" => ((Delegate)args![0]!).DynamicInvoke(args[1]),
            "IsCurrentAdministratorAsync" => Task.FromResult(Allowed),
            "GetAssignmentRoleCodeAsync" => Task.FromResult<string?>("HR"),
            _ => throw new Exception("Unexpected authorization call: " + method.Name)
        });
        var repository = Proxy.Of<IEmployeeHiringRepository>((method, args) => method.Name switch
        {
            "FindAsync" => Task.FromResult<Employee?>(Employee),
            "FindDraftAsync" => Task.FromResult<EmployeeHiringDraft?>(null),
            "FindDocumentAsync" => Task.FromResult<EmployeeDocument?>(args![1]!.Equals(Document.Id) ? Document : null),
            "AddDocument" => AddDocument((EmployeeDocument)args![0]!),
            "AddAudit" => AddAudit((AuditEvent)args![0]!),
            "ExpectVersion" => Expected(Employee.RowVersion, (byte[])args![1]!, "employee_version_conflict"),
            "FindReceiptAsync" => Task.FromResult(Receipts.SingleOrDefault(r => r.ActorUserId == (string)args![0]! && r.Operation == (string)args[1]! && r.Key == (string)args[2]!)),
            "AddReceipt" => AddReceipt((IdempotencyReceipt)args![0]!),
            "SaveChangesAsync" => Saved(),
            _ => throw new Exception("Unexpected hiring repository call: " + method.Name)
        });
        var documents = Proxy.Of<IEmployeeDocumentRepository>((method, args) => method.Name switch
        {
            "ExpectVersion" => Expected(((EmployeeDocument)args![0]!).RowVersion, (byte[])args[1]!, "employee_document_version_conflict"),
            "RefreshAsync" => Task.CompletedTask,
            "AddHistory" => AddHistory((EmployeeDocumentHistory)args![0]!),
            _ => throw new Exception("Unexpected document repository call: " + method.Name)
        });
        var files = Proxy.Of<IPrivateFileStore>((method, args) => method.Name switch
        {
            "ReadAsync" => Read(), "WriteAsync" => Write((byte[])args![0]!, (string)args[1]!),
            _ => throw new Exception("Unexpected store call: " + method.Name)
        });
        var scanner = Proxy.Of<IEmployeeFileScanner>((_, args) => { ScanCount++; return Task.FromResult(Scan((byte[])args![0]!)); });
        var clock = new Clock(Now);
        var operation = new EmployeeOperation(current, access, auth, repository, clock, files);
        Service = new(operation, repository, files, scanner, clock, documents, null!, null!, new() { BusinessTimeZoneId = "Asia/Karachi" });
    }
    private int savedRevision;
    private Task Saved()
    {
        if (Document.HistoryRevision != savedRevision)
        {
            savedRevision = Document.HistoryRevision;
            typeof(EmployeeDocument).GetProperty("RowVersion")!.SetValue(Document, BitConverter.GetBytes((long)savedRevision + 1));
        }
        return Task.CompletedTask;
    }
    private Task<byte[]> Read() { ReadCount++; LastRead = Bytes.ToArray(); DuringRead?.Invoke(this); return Task.FromResult(LastRead); }
    private Task<PrivateFileReference> Write(byte[] bytes, string purpose)
    {
        WriteCount++; if (purpose.EndsWith("/document")) ObjectBytes = bytes.ToArray();
        return Task.FromResult(new PrivateFileReference(Guid.NewGuid().ToString("N"), Guid.NewGuid().ToString("N"),
            Convert.ToHexString(SHA256.HashData(bytes)), "synthetic", bytes.LongLength));
    }
    private object? AddDocument(EmployeeDocument d) { AddedDocument = d; return null; }
    private object? AddAudit(AuditEvent audit) { Audits.Add(audit); return null; }
    private object? AddHistory(EmployeeDocumentHistory history) { History.Add(history); return null; }
    private object? AddReceipt(IdempotencyReceipt receipt) { Receipts.Add(receipt); return null; }
    private object? Expected(byte[] actual, byte[] expected, string code)
    { if (!actual.SequenceEqual(expected)) throw EmployeeOperation.Conflict(code); return null; }
}
class Clock(DateTimeOffset now) : TimeProvider { public override DateTimeOffset GetUtcNow() => now; }
class Proxy : DispatchProxy
{
    public Func<MethodInfo, object?[]?, object?> Handler = null!;
    public static T Of<T>(Func<MethodInfo, object?[]?, object?> handler) where T : class
    { var value = Create<T, Proxy>(); ((Proxy)(object)value).Handler = handler; return value; }
    protected override object? Invoke(MethodInfo? method, object?[]? args) => Handler(method!, args);
}
