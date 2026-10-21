#:project ../../src/Cosmix.Infrastructure/Cosmix.Infrastructure.csproj
#:property PublishAot=false
#:property RestorePackagesWithLockFile=false
using System.Reflection;
using System.Data.Common;
using System.Security.Cryptography;
using System.Text;
using Cosmix.Application.Attendance.Common;
using Cosmix.Application.Attendance.LegalReview;
using Cosmix.Application.Attendance.Shifts;
using Cosmix.Application.Authentication;
using Cosmix.Application.Authentication.Access;
using Cosmix.Application.Common.Files;
using Cosmix.Application.HR.Policies;
using Cosmix.Domain.Attendance.LegalReview;
using Cosmix.Domain.Authentication;
using Cosmix.Domain.Common.Audit;
using Cosmix.Domain.Common.Idempotency;
using Cosmix.Domain.HR.EmployeeDocuments;
using Cosmix.Domain.HR.Policies;
using Cosmix.Infrastructure.Repositories.Attendance;
using Cosmix.Infrastructure.Database;
using Cosmix.Domain.HR.EmployeeAssignments;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using UglyToad.PdfPig.Writer;

// Actual service + AttendanceOperation; only persistence/authentication/provider boundaries are in memory.
var checks = 0;
void Check(bool value, string name) { if (!value) throw new Exception(name); checks++; }
async Task Reject(Func<Task> action, string code)
{
    try { await action(); }
    catch (AuthenticationFailure e) when (e.Code == code) { checks++; return; }
    throw new Exception($"Expected {code}");
}
Check(AccessPermissionCatalog.Find(AttendanceLegalReviewPermissions.Record) is not null &&
    AccessPermissionCatalog.Find(AttendanceLegalReviewPermissions.Read) is not null,
    "Root must integrate dedicated permission definitions before executing this harness");
var rig = new Rig();
var request = rig.Request();
var first = await rig.Record(request, "legal-review-0001");
Check(first.State == "review_required" && !first.ActivationAllowed &&
    first.VerificationMode == "recorded_not_legally_verified", "Evidence recording cannot become clearance");
Check(first.Revision == 1 && first.SupersedesId is null && rig.Rows.Count == 1,
    "Initial dossier is retained once");
Check(first.SignedPdf.Sha256 == Rig.Hash(request.SignedSupportingPdf) &&
    first.HrProof.Sha256 == Rig.Hash(request.HrSupportingPdf), "Server hashes actual uploaded PDFs");
Check(rig.Scanner.Calls == 2 && rig.Files.Writes == 5, "Both Clean PDFs precede protected dossier/audit/receipt storage");
var retry = await rig.Record(request, "legal-review-0001");
Check(retry.Id == first.Id && rig.Rows.Count == 1 && rig.Scanner.Calls == 2,
    "Exact replay requires current authorization but avoids scanning/storage and duplicate dossier");
await Reject(() => rig.Record(request with { HrWrittenEvidence = "Changed HR assertion" }, "legal-review-0001"),
    "idempotency_key_reused");
await Reject(() => rig.Record(request, "legal-review-0002"), "legal_review_revision_conflict");
var next = request with { ExpectedRevision = 1, SupersedesId = first.Id, HrWrittenEvidence = "Corrected dated facts" };
var second = await rig.Record(next, "legal-review-0003");
Check(second.Revision == 2 && second.SupersedesId == first.Id && rig.Rows[0].Revision == 1,
    "Supersession appends without rewriting original");
await Reject(() => rig.Record(next, "legal-review-0004"), "legal_review_revision_conflict");
var history = await rig.Service.HistoryAsync(rig.Policy.Id, rig.Version.Id, first.BindingSha256, rig.Context, default);
Check(history.Count == 2 && history.All(x => !x.ActivationAllowed), "History remains record-only");
Check(Rig.Hash(await rig.Service.PdfAsync(rig.Policy.Id, rig.Version.Id, first.BindingSha256, first.Id, "signed", rig.Context, default)) == first.SignedPdf.Sha256,
    "Authorized download reads exact original PDF bytes");
Check(!System.Text.Json.JsonSerializer.Serialize(first).Contains("StorageKey", StringComparison.Ordinal),
    "Client DTO excludes private storage locators");
var writes = rig.Files.Writes;
rig.Grants.Clear();
await Reject(() => rig.Record(request, "legal-review-0001"), "access_denied");
Check(rig.Files.Writes == writes, "Revoked permission denies replay without provider/storage work");
rig.Allow(); rig.Identity = rig.Identity with { MfaSatisfied = false };
await Reject(() => rig.Record(request, "legal-review-0001"), "access_denied");
rig.Identity = rig.Identity with { MfaSatisfied = true, Client = SessionClient.Mobile };
await Reject(() => rig.Record(request, "legal-review-0001"), "access_denied");
rig.Identity = rig.Identity with { Client = SessionClient.Web };
rig.Grants.Clear(); rig.Grants.Add(new(AttendanceLegalReviewPermissions.Record, true, rig.Assignment,
    AccessScopeKind.Employee, rig.Identity.EmployeeId));
await Reject(() => rig.Record(request, "legal-review-0001"), "access_denied");
rig.Allow();
await Reject(() => rig.Record(request with { ExpectedPolicySha256 = new string('A', 64) }, "legal-review-hash"),
    "legal_review_policy_changed");
await Reject(() => rig.Service.RecordAsync(rig.Policy.Id, Guid.NewGuid(), request, "legal-review-version", rig.Context, default),
    "legal_review_policy_version_not_found");
await Reject(() => rig.Record(request with { ReviewerIdentity = "x\nforged" }, "legal-review-text"),
    "invalid_legal_review_request");
await Reject(() => rig.Record(request with { SignedSupportingPdf = new byte[AttendanceLegalReviewService.MaximumPdfBytes + 1] },
    "legal-review-size"), "invalid_legal_review_pdf");
await Reject(() => rig.Record(next with { ExpectedRevision = 2, SupersedesId = second.Id,
    SignedSupportingPdf = Encoding.ASCII.GetBytes("%PDF-1.7\nnot a PDF\n%%EOF") },
    "legal-review-fake"), "invalid_legal_review_pdf");
Check(rig.Files.Writes == writes, "Rejected evidence has no private writes");
rig.Scanner.State = DocumentScanStatus.Rejected;
await Reject(() => rig.Record(next with { ExpectedRevision = 2, SupersedesId = second.Id }, "legal-review-infected"),
    "legal_review_pdf_not_clean");
Check(rig.Files.Writes == writes, "Non-Clean scan prevents any evidence store");
rig.Scanner.State = DocumentScanStatus.Clean;
rig.ScopeChanged = false;
rig.Scanner.AfterScan = () => rig.ScopeChanged = true;
await Reject(() => rig.Record(next with { ExpectedRevision = 2, SupersedesId = second.Id }, "legal-review-scope-race"),
    "legal_review_scope_evidence_changed");
Check(rig.Files.Writes == writes, "Scope change during provider I/O prevents private writes");
rig.ScopeChanged = false;
rig.Scanner.AfterScan = () => rig.Grants.Clear();
await Reject(() => rig.Record(next with { ExpectedRevision = 2, SupersedesId = second.Id }, "legal-review-revoke"),
    "access_denied");
Check(rig.Files.Writes == writes, "Authorization rechecked after scanner I/O");
rig.Scanner.AfterScan = null; rig.Allow();
rig.ScopeAvailable = false;
await Reject(() => rig.Record(request with { Scope = request.Scope with { EmployeeId = Guid.NewGuid(), StaffCategoryId = Guid.NewGuid() } },
    "legal-review-scope"), "legal_review_scope_evidence_unavailable");
rig.ScopeAvailable = true;
rig.Files.Tamper = true;
await Reject(() => rig.Service.HistoryAsync(rig.Policy.Id, rig.Version.Id, first.BindingSha256, rig.Context, default),
    "legal_review_evidence_invalid");
rig.Files.Tamper = false;
rig.Files.FailWrites = true;
await Reject(() => rig.Record(next with { ExpectedRevision = 2, SupersedesId = second.Id }, "legal-review-storage"),
    "private_storage_unavailable");
Check(rig.Rows.Count == 2, "Storage failure commits no dossier");
rig.Files.FailWrites = false;
// A distinct rule version cannot inherit the prior dossier chain, even if its rules happen to match.
rig.Version = new(rig.Policy.Id, 2, new(2026, 10, 1), null, "attendance.v1", rig.Version.RulesJson, null, null,
    "reported publisher", rig.Now, PolicyAuthority.DirectHr);
var replacement = await rig.Record(request, "legal-review-new-version");
Check(replacement.Revision == 1 && replacement.BindingSha256 != first.BindingSha256 && !replacement.ActivationAllowed,
    "New policy version opens separate record-only chain");
await Reject(() => rig.Service.HistoryAsync(rig.Policy.Id, rig.Version.Id, first.BindingSha256, rig.Context, default),
    "legal_review_evidence_invalid");

var guard = new NoLegalReviewConnections();
using var db = new CosmixDbContext(new DbContextOptionsBuilder<CosmixDbContext>()
    .UseSqlServer("Server=legal-review.invalid;Database=OfflineOnly;Integrated Security=true;Encrypt=true")
    .AddInterceptors(guard).Options);
var model = db.GetService<IDesignTimeModel>().Model;
var entity = model.FindEntityType(typeof(AttendanceLegalReviewDossier))!;
Check(entity.GetTableName() == "AttendanceLegalReviewDossiers" && entity.GetSchema() == "attendance", "Dedicated evidence table");
Check(entity.FindProperty("ActivationAllowed") is null && entity.FindProperty("ReviewerIdentity") is null,
    "No persisted activation switch or public reviewer data in headers");
Check(entity.GetIndexes().Any(x => x.IsUnique && x.Properties.Select(p => p.Name).SequenceEqual(new[] { "BindingSha256", "Revision" })),
    "Concurrent revision uniqueness");
Check(entity.GetIndexes().Any(x => x.IsUnique && x.Properties.Count == 1 && x.Properties[0].Name == "SupersedesId"), "Only one successor");
Check(entity.GetForeignKeys().All(x => x.DeleteBehavior == DeleteBehavior.Restrict), "Evidence deletion never cascades");
var repo = new AttendanceLegalReviewRepository(db,
    Proxy<Cosmix.Application.HR.Employees.IEmployeeSiteAssignmentRepository>.Create((m, _) => throw new Exception(m.Name)),
    Proxy<Cosmix.Application.Attendance.PayrollInputs.IAttendancePayrollJurisdictionReader>.Create((m, _) => throw new Exception(m.Name)));
var historyQuery = (IQueryable<AttendanceLegalReviewDossier>)typeof(AttendanceLegalReviewRepository)
    .GetMethod("HistoryQuery", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(repo, [first.BindingSha256])!;
var historySql = historyQuery.ToQueryString();
Check(historySql.Contains("BindingSha256", StringComparison.Ordinal) && historySql.Contains("TOP", StringComparison.Ordinal) &&
    historySql.Contains("ORDER BY", StringComparison.Ordinal), "Actual repository history query translates with binding/order/limit");
var assignmentQuery = (IQueryable<EmployeeAssignment>)typeof(AttendanceLegalReviewRepository)
    .GetMethod("EmploymentScopeQuery", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(repo,
        [rig.Identity.EmployeeId, new DateOnly(2026, 10, 1), new DateOnly(2026, 11, 1)])!;
var scopeSql = assignmentQuery.ToQueryString();
Check(scopeSql.Contains("EffectiveFrom", StringComparison.Ordinal) && scopeSql.Contains("EffectiveTo", StringComparison.Ordinal) &&
    scopeSql.Contains("EmployeeId", StringComparison.Ordinal) && scopeSql.Contains("TOP", StringComparison.Ordinal),
    "Actual repository bounded employee/interval query translates offline");
db.Attach(rig.Rows[0]); db.Remove(rig.Rows[0]);
try { db.SaveChanges(); throw new Exception("Deletion accepted"); }
catch (InvalidOperationException e) when (e.Message.Contains("append-only", StringComparison.Ordinal)) { checks++; }
db.ChangeTracker.Clear(); db.Attach(rig.Rows[0]);
db.Entry(rig.Rows[0]).Property(x => x.ProtectedEvidenceReference).CurrentValue = "replacement";
try { db.SaveChanges(); throw new Exception("Replacement accepted"); }
catch (InvalidOperationException e) when (e.Message.Contains("append-only", StringComparison.Ordinal)) { checks++; }
Check(guard.Attempts == 0, "Model, query translation and append-only checks open no SQL connection");
Console.WriteLine($"PASS: {checks} actual legal-review service checks. In-memory boundaries only; no SQL, live CSRF, signature or legal verification.");

public sealed class Rig
{
    public DateTimeOffset Now = new(2026, 10, 12, 12, 0, 0, TimeSpan.Zero);
    public Guid Assignment = Guid.NewGuid();
    public AuthenticatedAccount Identity = new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 1, SessionClient.Web, true);
    public List<PermissionGrant> Grants = [];
    public List<AttendanceLegalReviewDossier> Rows = [];
    public Dictionary<string, IdempotencyReceipt> Receipts = [];
    public List<AuditEvent> Audit = [];
    public Policy Policy = new("REVIEW", "Synthetic review source", PolicyCategory.Attendance);
    public PolicyVersion Version;
    public bool ScopeAvailable = true;
    public bool ScopeChanged;
    public MemoryFiles Files = new();
    public MemoryScanner Scanner = new();
    public AuthRequestContext Context = new("", "offline-legal-review");
    public AttendanceLegalReviewService Service;
    public Rig()
    {
        // Construct valid company rules through the codec instead of relying on copied enum names.
        var rules = new AttendancePolicyRules(1, new(0, 10, 20),
            new(Enum.GetValues<AttendancePaidTimeBasis>()[0], Enum.GetValues<AttendanceBreakTreatment>()[0],
                Enum.GetValues<AttendanceEarlyExitTreatment>()[0], 100, Enum.GetValues<AttendanceNonScheduledDayTreatment>()[0],
                Enum.GetValues<AttendanceNonScheduledDayTreatment>()[0]),
        new(3, AttendanceLateResetPeriod.CalendarMonth, false, false, AttendanceDeductionSalaryBasis.BaseSalary,
                AttendanceSalaryDivisor.ActualCalendarMonthDays, null, 1m, Enum.GetValues<AttendanceDeductionRounding>()[0]));
        Version = new(Policy.Id, 1, new(2026, 10, 1), null, "attendance.v1", AttendancePolicyRulesCodec.Canonicalize(rules),
            null, null, "reported publisher", Now, PolicyAuthority.DirectHr);
        Allow();
        var current = Proxy<ICurrentAuthentication>.Create((_, _) => Identity);
        var access = Proxy<IAccountAccessReader>.Create((_, _) => Task.FromResult<IReadOnlyList<PermissionGrant>>(Grants.ToArray()));
        var gate = Proxy<IAccessManagementRepository>.Create((method, args) => method.Name switch
        {
            "InTransactionAsync" => ((Delegate)args[0]!).DynamicInvoke(args[1]),
            "GetAssignmentRoleCodeAsync" => Task.FromResult<string?>("EXPLICIT_RECORD_PERMISSION"),
            _ => throw new Exception(method.Name)
        });
        var journal = Proxy<ISchedulingJournalRepository>.Create((method, args) => method.Name switch
        {
            "IsCurrentSessionAsync" => Task.FromResult(true),
            "FindReceiptAsync" => Task.FromResult(Receipts.GetValueOrDefault((string)args[2]!)),
            "AddReceipt" => AddReceipt((IdempotencyReceipt)args[0]!),
            "AddAudit" => AddAudit((AuditEvent)args[0]!),
            "SaveChangesAsync" => Task.CompletedTask,
            _ => throw new Exception(method.Name)
        });
        var authentication = Proxy<IAuthenticationRepository>.Create((m, _) => throw new Exception(m.Name));
        var policies = Proxy<IAttendancePolicyRepository>.Create((method, args) => method.Name switch
        {
            "FindAsync" => Task.FromResult((Guid)args[0]! == Policy.Id ? Policy : null),
            "VersionsAsync" => Task.FromResult<IReadOnlyList<PolicyVersion>>([Version]),
            _ => throw new Exception(method.Name)
        });
        var repository = Proxy<IAttendanceLegalReviewRepository>.Create((method, args) => method.Name switch
        {
            "HistoryAsync" => Task.FromResult<IReadOnlyList<AttendanceLegalReviewDossier>>(Rows.Where(x => x.BindingSha256 == (string)args[0]!).OrderBy(x => x.Revision).ToArray()),
            "ScopeEvidenceAsync" => Task.FromResult<LegalReviewScopeEvidence?>(ScopeAvailable ? new((LegalReviewScope)args[0]!,
                ScopeChanged ? "changed" : "", null, null, null, null) : null),
            "Add" => AddRow((AttendanceLegalReviewDossier)args[0]!),
            _ => throw new Exception(method.Name)
        });
        Service = new(new AttendanceOperation(current, access, gate, journal, authentication, new FixedClock(Now), Files),
            repository, policies, Files, Scanner, new AttendanceLegalReviewPdfInspector(), new FixedClock(Now));
    }
    object? AddRow(AttendanceLegalReviewDossier row) { Rows.Add(row); return null; }
    object? AddReceipt(IdempotencyReceipt row) { Receipts.Add(row.Key, row); return null; }
    object? AddAudit(AuditEvent row) { Audit.Add(row); return null; }
    public void Allow()
    {
        Grants.Clear();
        foreach (var p in new[] { AttendanceLegalReviewPermissions.Read, AttendanceLegalReviewPermissions.Record })
            Grants.Add(new(p, true, Assignment, AccessScopeKind.All, null));
    }
    public RecordLegalReviewRequest Request()
    {
        var pdf = new PdfDocumentBuilder(); pdf.AddPage(UglyToad.PdfPig.Content.PageSize.A4);
        return new(Hash(Encoding.UTF8.GetBytes(AttendancePolicyRulesCodec.ValidateAndCanonicalize(Version.RulesJson).CanonicalJson!)),
            "PK-SD", new("reported worker category", "reported establishment scope", null, null, null, null),
            new(2026, 10, 1), new(2026, 11, 1), 0, null, "reported counsel", "reported qualification",
            Now.AddDays(-1), "rules and commencement evidence for independent review", "reported HR author",
            Now.AddDays(-1), "Dated HR applicability assertion", "contract and site records described by recorder",
            "signed.pdf", pdf.Build(), "hr-proof.pdf", pdf.Build(), "Record evidence for review");
    }
    public Task<LegalReviewResponse> Record(RecordLegalReviewRequest request, string key) =>
        Service.RecordAsync(Policy.Id, Version.Id, request, key, Context, default);
    public static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes));
}
public sealed class FixedClock(DateTimeOffset now) : TimeProvider { public override DateTimeOffset GetUtcNow() => now; }
public class Proxy<T> : DispatchProxy where T : class
{
    public Func<MethodInfo, object?[], object?> Handler = null!;
    public static T Create(Func<MethodInfo, object?[], object?> handler)
    { var proxy = DispatchProxy.Create<T, Proxy<T>>(); ((Proxy<T>)(object)proxy).Handler = handler; return proxy; }
    protected override object? Invoke(MethodInfo? method, object?[]? args) => Handler(method!, args!);
}
public sealed class MemoryScanner : IEmployeeFileScanner
{
    public DocumentScanStatus State = DocumentScanStatus.Clean; public int Calls; public Action? AfterScan;
    public Task<FileScanResult> ScanAsync(byte[] content, CancellationToken ct)
    { Calls++; AfterScan?.Invoke(); return Task.FromResult(new FileScanResult(State, "offline-scanner")); }
}
public sealed class MemoryFiles : IPrivateFileStore
{
    public int Writes; public bool Tamper; public bool FailWrites;
    private readonly Dictionary<string, (string Purpose, byte[] Bytes)> values = [];
    public Task<PrivateFileReference> WriteAsync(byte[] content, string purpose, CancellationToken ct)
    {
        if (FailWrites) throw new AuthenticationFailure("private_storage_unavailable", 503);
        Writes++; var key = Guid.NewGuid().ToString("N"); values.Add(key, (purpose, content.ToArray()));
        return Task.FromResult(new PrivateFileReference(key, "1", Rig.Hash(content), "offline", content.Length));
    }
    public Task<byte[]> ReadAsync(PrivateFileReference reference, string purpose, CancellationToken ct)
    {
        var stored = values[reference.StorageKey]; if (stored.Purpose != purpose) throw new Exception("Purpose mismatch");
        var bytes = stored.Bytes.ToArray(); if (Tamper) bytes[0] ^= 1; return Task.FromResult(bytes);
    }
}
public sealed class NoLegalReviewConnections : DbConnectionInterceptor
{
    public int Attempts;
    public override InterceptionResult ConnectionOpening(DbConnection connection, ConnectionEventData data, InterceptionResult result)
    { Attempts++; throw new Exception("SQL connections forbidden"); }
    public override ValueTask<InterceptionResult> ConnectionOpeningAsync(DbConnection connection, ConnectionEventData data,
        InterceptionResult result, CancellationToken ct = default)
    { Attempts++; throw new Exception("SQL connections forbidden"); }
}
