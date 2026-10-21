#:project ../../src/Cosmix.Application/Cosmix.Application.csproj
#:property PublishAot=false
#:property RestorePackagesWithLockFile=false
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using Cosmix.Application.Attendance.Common;
using Cosmix.Application.Attendance.Kpi;
using Cosmix.Application.Attendance.DailySummaries;
using Cosmix.Application.Attendance.Shifts;
using Cosmix.Application.Authentication;
using Cosmix.Application.Authentication.Access;
using Cosmix.Application.Common.Files;
using Cosmix.Application.Common.Notifications;
using Cosmix.Domain.Authentication;
using Cosmix.Domain.Common.Approvals;
using Cosmix.Domain.Common.Audit;
using Cosmix.Domain.Common.Idempotency;
using Cosmix.Domain.Attendance.Kpi;
using Cosmix.Domain.HR.Policies;

var checks = 0;
void Check(bool value, string reason) { if (!value) throw new Exception(reason); checks++; }
async Task Failure(Func<Task> action, int status, string code) {
    try { await action(); throw new Exception("Expected failure: " + code); }
    catch (AuthenticationFailure e) { Check(e.StatusCode == status && e.Code == code, code); }
}
var f = new Fixture();
var request = new PrepareKpiRequest(f.AsOf, f.Source.Fingerprint, null, "Synthetic complete source review");
var submitted = await f.Service.PrepareAsync(f.Employee, request, "kpi-prepare-0001", f.Context, default);
Check(submitted.State == "Pending" && submitted.FinalizationId is null && f.FinalAudits.Count == 0, "Prepared source not finalized.");
var replay = await f.Service.PrepareAsync(f.Employee, request, "kpi-prepare-0001", f.Context, default);
Check(replay.Id == submitted.Id && f.Rows.Count == 1, "Same-key retry one request.");
await Failure(() => f.Service.PrepareAsync(f.Employee, request with { Reason = "Changed payload" }, "kpi-prepare-0001", f.Context, default), 409, "idempotency_key_reused");
await Failure(() => f.Service.PrepareAsync(f.Employee, request, "kpi-prepare-0002", f.Context, default), 409, "kpi_period_review_pending");
await Failure(() => f.Service.DecideAsync(f.Employee, submitted.Id, new(submitted.Version, ApprovalState.Approved, "Self review"), "kpi-decide-0001", f.Context, default), 403, "access_denied");
f.Identity = f.Identity with { AccountId = Guid.NewGuid() };
await Failure(() => f.Service.DecideAsync(f.Employee, submitted.Id, new(Convert.ToBase64String(new byte[8]), ApprovalState.Approved, "Stale version"), "kpi-decide-stale", f.Context, default), 409, "stale_kpi_version");
var approved = await f.Service.DecideAsync(f.Employee, submitted.Id, new(submitted.Version, ApprovalState.Approved, "Independent synthetic review"), "kpi-decide-0002", f.Context, default);
Check(approved.State == "Approved" && approved.FinalizationId.HasValue && approved.FinalRevision == 1 && f.FinalAudits.Count == 1, "Exact annual revision finalized.");
var final = await f.Final.ReadAsync(f.Employee, f.Source.From, default);
Check(final?.ExactScore == 99.995m && final.DisplayScore == 100m && final.RequestId == submitted.Id, "Exact score retained independently of display.");
var repeatDecision = await f.Service.DecideAsync(f.Employee, submitted.Id, new(submitted.Version, ApprovalState.Approved, "Independent synthetic review"), "kpi-decide-0002", f.Context, default);
Check(repeatDecision.FinalizationId == approved.FinalizationId && f.FinalAudits.Count == 1, "Decision replay one finalization.");
Check(f.Notifications.Count == 1 && f.Notifications[0].ResourceId == submitted.Id &&
    f.Notifications[0].TemplateCode == NotificationTemplates.Decision, "Generic notification staged only once for real decision.");
await Failure(() => f.Service.DecideAsync(f.Employee, submitted.Id, new(approved.Version, ApprovalState.Approved, "Duplicate decision"), "kpi-decide-0003", f.Context, default), 409, "kpi_request_already_decided");
f.Grants.Clear();
await Failure(() => f.Service.GetAsync(f.Employee, submitted.Id, f.Context, default), 403, "access_denied");
f.AllowAll(); f.Identity = f.Identity with { MfaSatisfied = false };
await Failure(() => f.Service.PreviewAsync(f.Employee, f.AsOf, f.Context, default), 403, "access_denied");
f.Identity = f.Identity with { MfaSatisfied = true }; f.SessionCurrent = false;
await Failure(() => f.Service.GetAsync(f.Employee, submitted.Id, f.Context, default), 401, "invalid_authentication");
var g = new Fixture();
var own = new PrepareKpiRequest(g.AsOf, g.Source.Fingerprint, null, "Synthetic");
await Failure(() => g.Service.PrepareAsync(g.Employee, own with { ExpectedSourceFingerprint = new string('B', 64) }, "kpi-negative-001", g.Context, default), 409, "kpi_source_changed");
g.Source = g.Source with { Calculation = g.Source.Calculation with { State = "unavailable", ExactScore = null, DisplayScore = null } };
await Failure(() => g.Service.PrepareAsync(g.Employee, own, "kpi-negative-002", g.Context, default), 409, "kpi_verified_calculation_required");
var ytd = new Fixture();
var ytdInput = ytd.Source.SourceManifest!.Input with { ToExclusive = ytd.Source.From.AddDays(7), Days = ytd.Source.SourceManifest.Input.Days.Take(7).ToArray() };
ytd.Source = ytd.Source with { ToExclusive = ytdInput.ToExclusive, Calculation = AttendanceKpiCalculation.Calculate(ytdInput),
    SourceManifest = ytd.Source.SourceManifest with { Input = ytdInput, SavedDays = ytd.Source.SourceManifest.SavedDays.Take(7).ToArray() } };
await Failure(() => ytd.Service.PrepareAsync(ytd.Employee, new(ytd.AsOf, ytd.Source.Fingerprint, null, "Incomplete annual"), "kpi-ytd-0001", ytd.Context, default), 409, "kpi_annual_period_incomplete");
var race = new Fixture(); race.ChangeDuringWrite = true;
await Failure(() => race.Service.PrepareAsync(race.Employee, new(race.AsOf, race.Source.Fingerprint, null, "Source race"), "kpi-race-0001", race.Context, default), 409, "kpi_source_changed");
Check(race.Rows.Count == 0 && race.Receipts.Count == 0, "No request or receipt on pre-save source race.");
var storage = new Fixture(); storage.FailWrites = true;
await Failure(() => storage.Service.PrepareAsync(storage.Employee, new(storage.AsOf, storage.Source.Fingerprint, null, "Storage failure"), "kpi-storage-0001", storage.Context, default), 503, "private_storage_unavailable");
Check(storage.Rows.Count == 0 && storage.FinalAudits.Count == 0, "No fake source on storage failure.");
var corruption = new Fixture();
var cRequest = await corruption.Service.PrepareAsync(corruption.Employee, new(corruption.AsOf, corruption.Source.Fingerprint, null, "Synthetic annual"),
    "kpi-corrupt-prepare", corruption.Context, default);
corruption.Identity = corruption.Identity with { AccountId = Guid.NewGuid() };
await corruption.Service.DecideAsync(corruption.Employee, cRequest.Id, new(cRequest.Version, ApprovalState.Approved, "Synthetic review"),
    "kpi-corrupt-review", corruption.Context, default);
var requestRef = JsonSerializer.Deserialize<PrivateFileReference>(corruption.Rows.Single().SnapshotPrivateReference)!;
var original = corruption.Files[requestRef.StorageKey];
var recorded = JsonSerializer.Deserialize<KpiRequestSnapshot>(original.Bytes)!;
var shortInput = recorded.Source.SourceManifest!.Input with { ToExclusive = recorded.Source.From.AddDays(7), Days = recorded.Source.SourceManifest.Input.Days.Take(7).ToArray() };
var shortSource = recorded.Source with { ToExclusive = shortInput.ToExclusive, Calculation = AttendanceKpiCalculation.Calculate(shortInput),
    SourceManifest = recorded.Source.SourceManifest with { Input = shortInput, SavedDays = recorded.Source.SourceManifest.SavedDays.Take(7).ToArray() } };
corruption.Files[requestRef.StorageKey] = (original.Purpose, JsonSerializer.SerializeToUtf8Bytes(recorded with { AsOf = shortInput.ToExclusive.AddDays(-1), Source = shortSource }));
var finalRef = JsonSerializer.Deserialize<PrivateFileReference>(corruption.FinalAudits.Single().ProtectedChangesReference!)!;
var finalFile = corruption.Files[finalRef.StorageKey];
var finalHeader = JsonSerializer.Deserialize<FinalizedKpiScore>(finalFile.Bytes)!;
corruption.Files[finalRef.StorageKey] = (finalFile.Purpose, JsonSerializer.SerializeToUtf8Bytes(finalHeader with { CalculationFingerprint = shortSource.Calculation.Fingerprint }));
await Failure(() => corruption.Final.ReadAsync(corruption.Employee, corruption.Source.From, default), 409, "kpi_final_source_conflict");
Console.WriteLine($"C04 workflow: {checks} checks passed. Actual AttendanceOperation/service/private-purpose/receipt logic with synthetic in-memory adapters; not authenticated HTTP, SQL transactions or real encrypted-storage acceptance.");

public class Proxy : DispatchProxy {
    public Func<MethodInfo, object?[], object?> Handler = null!;
    protected override object? Invoke(MethodInfo? method, object?[]? args) => Handler(method!, args ?? []);
    public static T Make<T>(Func<MethodInfo, object?[], object?> handler) where T : class {
        var proxy = Create<T, Proxy>(); ((Proxy)(object)proxy).Handler = handler; return proxy;
    }
}
public sealed class Clock : TimeProvider { public override DateTimeOffset GetUtcNow() => new(2026, 10, 13, 10, 0, 0, TimeSpan.Zero); }
public sealed class Fixture {
    public Guid Employee = Guid.NewGuid();
    public DateOnly AsOf = new(2026, 6, 30);
    public AuthenticatedAccount Identity = new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 1, SessionClient.Web, true);
    public AuthRequestContext Context = new("127.0.0.1", "synthetic-c04");
    public bool SessionCurrent = true, FailWrites, ChangeDuringWrite;
    public KpiSource Source;
    public List<PermissionGrant> Grants = [];
    public List<ApprovalRequest> Rows = [];
    public List<ApprovalDecision> Decisions = [];
    public List<AuditEvent> FinalAudits = [], OperationAudits = [];
    public List<IdempotencyReceipt> Receipts = [];
    public List<NotificationIntent> Notifications = [];
    public Dictionary<string, (string Purpose, byte[] Bytes)> Files = [];
    public AttendanceKpiService Service;
    public FinalizedKpiReader Final;
    public void AllowAll() { foreach (var permission in KpiPermissions.Definitions) Grants.Add(new(permission.Code, true, Guid.NewGuid(), AccessScopeKind.All, null)); }
    public Fixture() {
        Source = new(Employee, new(2025, 7, 1), new(2026, 7, 1), Guid.NewGuid(),
            new("ready_for_review", 99.995m, 100m, 0, false, [], [], new string('C', 64)), new string('A', 64),
            [new([new(EmployeeId: Employee)])], [Guid.NewGuid()]);
        var configured = new AdvancedAttendanceRules(1, AdvancedAttendanceFamily.Kpi, ["SITE"],
            new("SYNTHETIC-C04", KpiPermissions.Decide, true, [AdvancedEvidenceKind.AcceptedDuty]),
            Kpi: new(100, 7, 2, AdvancedRounding.ToEven, 0, 100, KpiLateBasis.PercentOfBaseScore,
                .8m, .5m, KpiBeyondTierTreatment.ReviewRequired, null, KpiShortageTreatment.None, null, false, null) { Composition = KpiCompositionStrategy.Additive });
        var input = new KpiInput(configured, Source.From, Source.ToExclusive, Enumerable.Range(0, 365).Select(i =>
            new KpiDayInput(Source.From.AddDays(i), Guid.NewGuid(), new string('C', 64), false, null, null, null, i == 0 ? .005m : null)).ToArray(), []);
        var saved = input.Days.Select(x => new KpiSavedDay(x.Date, x.SummaryId, 1, "synthetic-saved-calculation", x.SourceFingerprint,
            Guid.NewGuid(), Guid.NewGuid(), Source.PolicyVersionId!.Value, Source.PolicyAssignmentIds, new string('E', 64))).ToArray();
        Source = Source with { Calculation = AttendanceKpiCalculation.Calculate(input), PolicyFingerprint = new string('E', 64),
            SourceManifest = new(AttendanceKpiCalculation.Version, input, saved, new string('F', 64)) };
        AllowAll();
        var authorization = Proxy.Make<IAccessManagementRepository>((method, args) => method.Name switch {
            "InTransactionAsync" => GetType().GetMethod(nameof(Transaction), BindingFlags.Instance | BindingFlags.NonPublic)!.MakeGenericMethod(method.GetGenericArguments()[0]).Invoke(this, args),
            "GetAssignmentRoleCodeAsync" => Task.FromResult<string?>("ExplicitKpiReviewer"), _ => throw new Exception(method.Name)
        });
        var journal = Proxy.Make<ISchedulingJournalRepository>((method, args) => method.Name switch {
            "IsCurrentSessionAsync" => Task.FromResult(SessionCurrent),
            "FindReceiptAsync" => Task.FromResult(Receipts.SingleOrDefault(x => x.ActorUserId == (string)args[0]! && x.Operation == (string)args[1]! && x.Key == (string)args[2]!)),
            "AddReceipt" => Add(Receipts, (IdempotencyReceipt)args[0]!), "AddAudit" => Add(OperationAudits, (AuditEvent)args[0]!),
            "SaveChangesAsync" => Task.CompletedTask, _ => throw new Exception(method.Name)
        });
        var files = Proxy.Make<IPrivateFileStore>((method, args) => {
            if (method.Name == "WriteAsync") {
                if (FailWrites) throw new AuthenticationFailure("private_storage_unavailable", 503);
                if (ChangeDuringWrite) Source = Source with { Fingerprint = new string('D', 64) };
                var id = Guid.NewGuid().ToString("N"); var bytes = (byte[])args[0]!; Files[id] = ((string)args[1]!, bytes);
                return Task.FromResult(new PrivateFileReference(id, "1", Convert.ToHexString(SHA256.HashData(bytes)), "synthetic", bytes.Length));
            }
            var stored = Files[((PrivateFileReference)args[0]!).StorageKey];
            if (stored.Purpose != (string)args[1]!) throw new Exception("Wrong private purpose"); return Task.FromResult(stored.Bytes);
        });
        var operation = new AttendanceOperation(Proxy.Make<ICurrentAuthentication>((_, _) => Identity),
            Proxy.Make<IAccountAccessReader>((_, _) => Task.FromResult<IReadOnlyList<PermissionGrant>>(Grants.ToArray())), authorization, journal,
            Proxy.Make<IAuthenticationRepository>((m, _) => throw new Exception(m.Name)), new Clock(), files);
        var repo = Proxy.Make<IKpiRepository>((method, args) => method.Name switch {
            "FindAsync" => Task.FromResult(Rows.SingleOrDefault(x => x.Id == (Guid)args[1]! && x.SubjectId == (Guid)args[0]!) is { } row ?
                new KpiRequestRow(row, Decisions.Where(x => x.ApprovalRequestId == row.Id).ToArray()) : null),
            "PendingAsync" => Task.FromResult<IReadOnlyList<ApprovalRequest>>(Rows.Where(x => x.State == ApprovalState.Pending).ToArray()),
            "FinalizationsAsync" => Task.FromResult<IReadOnlyList<AuditEvent>>(FinalAudits.ToArray()),
            "Add" => args[0] switch { ApprovalRequest a => Add(Rows, a), ApprovalDecision d => Add(Decisions, d), AuditEvent a => Add(FinalAudits, a), _ => throw new Exception("Unexpected entity") },
            "ExpectVersion" => Expect((ApprovalRequest)args[0]!, (byte[])args[1]!),
            "SaveChangesAsync" => Save(), _ => throw new Exception(method.Name)
        });
        var source = Proxy.Make<IKpiSourceReader>((method, _) => method.Name switch {
            "ReadAsync" => Task.FromResult(Source), "ScopesAsync" => Task.FromResult(Source.Scopes), _ => throw new Exception(method.Name)
        });
        var summaries = Proxy.Make<IAttendanceSummaryRepository>((method, _) => method.Name == "ResourceScopesAsync" ? Task.FromResult(Source.Scopes) : throw new Exception(method.Name));
        var notification = Proxy.Make<INotificationOutbox>((m, args) => { var intent = (NotificationIntent)args[0]!;
            Notifications.Add(intent); return Task.FromResult(true); });
        Final = new(repo, operation); Service = new(operation, repo, source, summaries, Final, files, new Clock(), notification);
    }
    private static object? Add<T>(List<T> list, T value) { list.Add(value); return null; }
    private static object? Expect(ApprovalRequest row, byte[] version) {
        if (!row.RowVersion.SequenceEqual(version)) throw new AuthenticationFailure("stale_kpi_version", 409); return null;
    }
    private Task Save() {
        foreach (var row in Rows) typeof(ApprovalRequest).GetProperty(nameof(ApprovalRequest.RowVersion))!.SetValue(row, BitConverter.GetBytes(1L));
        return Task.CompletedTask;
    }
    private async Task<T> Transaction<T>(Func<CancellationToken, Task<T>> action, CancellationToken ct) => await action(ct);
}
