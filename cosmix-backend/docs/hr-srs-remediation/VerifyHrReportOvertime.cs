using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using Cosmix.Application.Attendance.Common;
using Cosmix.Application.Attendance.Corrections;
using Cosmix.Application.Attendance.DailySummaries;
using Cosmix.Application.Attendance.Overtime;
using Cosmix.Application.Attendance.MonthLocks;
using Cosmix.Application.Attendance.Rosters;
using Cosmix.Application.Attendance.Shifts;
using Cosmix.Application.Attendance.WorkTreatments;
using Cosmix.Application.HR.Leaves;
using Cosmix.Application.Authentication;
using Cosmix.Application.Authentication.Access;
using Cosmix.Application.Common.Files;
using Cosmix.Application.Common.Notifications;
using Cosmix.Application.HR.Compensation;
using Cosmix.Domain.Attendance.Overtime;
using Cosmix.Domain.Attendance.WorkSessions;
using Cosmix.Domain.Authentication;
using Cosmix.Domain.Common.Approvals;
using Cosmix.Domain.Common.Idempotency;
using Cosmix.Domain.HR.Compensation;


using Cosmix.Application.HR.Reports;
using Cosmix.Domain.Common.Reports;
using System.Text;

// Standalone report integration with the retained synthetic OT fixture from VerifyOvertimeService.
// Actual application services and protected evidence validation execute; no SQL/host/provider is used.
var count = 0;
void Check(bool value, string name) { count++; if (!value) throw new Exception(name); }
async Task Denied(Func<Task> action, string code)
{
    count++; try { await action(); } catch (AuthenticationFailure ex) when (ex.Code == code) { return; }
    throw new Exception("Expected " + code);
}
var f = new OtFixture();
f.EligibleOverride = [new(f.ScheduleEnd, f.End.AddMinutes(-30))];
var pending = await f.Submit(await f.Proposal());
f.Reviewer();
var approved = await f.Decide(pending, ApprovalState.Approved);
Check(approved.RequestedMinutes == 120 && approved.ApprovedMinutes == 90, "source fixture independently retains partial award");
var allPermissions = HrReportAuthorization.RequiredPermissions(HrReportTypes.Overtime, true);
var grants = allPermissions.Select(p => new PermissionGrant(p, true, Guid.NewGuid(), AccessScopeKind.All, null)
    { AuthorityTimingKnown = true, ValidFromUtc = f.Clock.GetUtcNow().AddDays(-1) }).ToList();
var current = OtProxy.Make<ICurrentAuthentication>((m,a) => f.Actor);
var access = OtProxy.Make<IAccountAccessReader>((m,a) => Task.FromResult<IReadOnlyList<PermissionGrant>>(grants.ToArray()));
var transactions = 0; var audits = 0;
var authz = OtProxy.Make<IAccessManagementRepository>((m,a) => m.Name switch
{
    "InTransactionAsync" => Transaction((Delegate)a[0]!, a[1]),
    "GetAssignmentRoleCodeAsync" => Task.FromResult<string?>("ReportReviewer"),
    _ => throw new Exception(m.Name)
});
object? Transaction(Delegate action, object? token)
{
    if (transactions != 0) throw new Exception("nested transaction");
    transactions++;
    try { var result = action.DynamicInvoke(token); if (result is Task task) _ = task.ContinueWith(_ => transactions--, TaskContinuationOptions.ExecuteSynchronously); return result; }
    catch { transactions--; throw; }
}
var journal = OtProxy.Make<ISchedulingJournalRepository>((m,a) => m.Name switch
{
    "IsCurrentSessionAsync" => Task.FromResult(f.SessionCurrent),
    "AddAudit" => Audit(),
    "SaveChangesAsync" => Task.CompletedTask,
    _ => throw new Exception(m.Name)
});
object? Audit() { audits++; return null; }
var reportRepo = OtProxy.Make<IHrReportRepository>((m,a) => m.Name switch
{
    "SelectScopeAsync" => Task.FromResult(new HrReportSelection([f.Employee], false)
        { ReadRoleAssignmentIds = grants.Where(g => g.Permission == HrReportPermissions.Read).Select(g => g.RoleAssignmentId).ToArray() }),
    "GetOvertimeAsync" => Task.FromResult<IReadOnlyList<OvertimeReportRow>>(new HrReportRows<OvertimeReportRow>(f.Rows.Select(x =>
        new OvertimeReportRow(x.Request.Id, x.Request.RequestedAtUtc, 123456, "Synthetic OT", "Actual site", x.Request.WorkSessionId,
            x.Request.StartsAtUtc, x.Request.EndsAtUtc, x.Request.RequestedMinutes, x.Request.RateRuleCode, x.Request.JobReference,
            x.Request.Reason, x.Request.RequestedBy, x.Approval.State.ToString(), null, null) { EmployeeId = x.Request.EmployeeId }).ToArray(), f.Rows.Count)),
    _ => throw new Exception(m.Name)
});
var reportAuth = new HrReportAuthorization(current, access, authz, journal, reportRepo, f.FileStore, f.Clock);
var report = new HrReportService(reportRepo, reportAuth, f.Clock, f.Service, f.Operation);
var filter = new HrReportFilter(From: f.Date, To: f.Date.AddDays(2));
async Task<OvertimeReportRow> Read() => ((HrReportEnvelope<OvertimeReportRow>)await report.GetReportDataAsync(HrReportTypes.Overtime, filter, f.Context, default)).Rows.Single();
var row = await Read();
Check(row.RequestedMinutes == 120 && row.ApprovedMinutes == 90 && row.ApprovedTicks == TimeSpan.FromMinutes(90).Ticks, "report uses retained 90-minute award, not 120 requested");
Check(row.PayableAmount == 538m && row.Currency == "PKR" && row.DecisionId == approved.DecisionId, "report exact retained PKR 538 and decision identity");
Check(row.FinancialState == "calculated_pending_payroll" && audits == 1 && transactions == 0, "calculation not settlement; one outer transaction/audit");
var csv = Encoding.UTF8.GetString((await report.ExportReportAsync(HrReportTypes.Overtime,
    new(ReportFormat.Csv, "Synthetic parity", filter), f.Context, default)).Content);
Check(csv.Contains("\"120\",\"90\",\"538\"", StringComparison.Ordinal) && csv.Contains("\"PKR\",\"calculated_pending_payroll\"", StringComparison.Ordinal), "actual CSV independently expected financial cells");
f.Salary = new CompensationRecord(f.Employee, 2, f.Salary.Id, 999999, 999999, "PKR", WageBasis.Monthly,
    PaymentMethod.Bank, f.Date.AddMonths(1), null, null, "future-salary", f.Clock.GetUtcNow());
Check((await Read()).PayableAmount == 538m, "future salary does not reprice retained award");
var financialGrant = grants.Single(x => x.Permission == OvertimePermissions.FinancialRead);
grants.Remove(financialGrant);
await Denied(() => Read(), "access_denied");
grants.Add(financialGrant);
var exportGrant = grants.Single(x => x.Permission == HrReportPermissions.Export);
grants.Remove(exportGrant);
await Denied(() => report.ExportReportAsync(HrReportTypes.Overtime, new(ReportFormat.Csv, "Synthetic", filter), f.Context, default), "access_denied");
grants.Add(exportGrant);
var readGrant = grants.Single(x => x.Permission == HrReportPermissions.Read);
grants.Remove(readGrant);
grants.Add(readGrant with { ScopeKind = AccessScopeKind.Site, TargetId = Guid.NewGuid() });
await Denied(() => Read(), "access_denied");
grants.RemoveAll(x => x.Permission == HrReportPermissions.Read); grants.Add(readGrant);
var evidenceRef = JsonSerializer.Deserialize<PrivateFileReference>(f.Rows.Single().Decision!.ProtectedChangeReference!)!;
var original = f.Files[evidenceRef.StorageKey];
var decision = JsonSerializer.Deserialize<OvertimeDecisionSnapshot>(original.Data)!;
f.Files[evidenceRef.StorageKey] = (original.Purpose, JsonSerializer.SerializeToUtf8Bytes(decision with
    { FinancialEvidence = decision.FinancialEvidence! with { Amount = 1m } }));
await Denied(() => Read(), "overtime_decision_evidence_unavailable");
f.Files[evidenceRef.StorageKey] = original;
Check((await Read()).PayableAmount == 538m, "valid retained evidence still readable after rejected corruption");
f.SessionCurrent = false;
await Denied(() => Read(), "invalid_authentication");
Console.WriteLine($"HR overtime report retained integration: {count}/{count} passed. Synthetic collaborators only; no SQL, real file storage, HTTP or providers.");
return 0;

public sealed class OtClock : TimeProvider
{
    public DateTimeOffset Now = new(2026, 10, 10, 16, 0, 0, TimeSpan.Zero);
    public override DateTimeOffset GetUtcNow() => Now;
}
public class OtProxy : DispatchProxy
{
    public Func<MethodInfo, object?[], object?> Handler = null!;
    protected override object? Invoke(MethodInfo? method, object?[]? args) => Handler(method!, args ?? []);
    public static T Make<T>(Func<MethodInfo, object?[], object?> handler) where T : class
    { var proxy = Create<T, OtProxy>(); ((OtProxy)(object)proxy).Handler = handler; return proxy; }
}
public sealed class OtFixture
{
    public IReadOnlyList<MonthLockOvertimeDependency> LockDependencies() => Rows.Select(row => {
        var source=Sessions.Single(x=>x.Id==row.Request.WorkSessionId);
        // A persisted header projection for the synthetic source, not a punch/correction write.
        var session=(WorkSession)Activator.CreateInstance(typeof(WorkSession),nonPublic:true)!;
        foreach(var property in typeof(OvertimeSessionSource).GetProperties())
            typeof(WorkSession).GetProperty(property.Name)?.SetValue(session,property.GetValue(source));
        typeof(WorkSession).GetProperty(nameof(WorkSession.EmployeeId))!.SetValue(session,Employee);
        typeof(WorkSession).GetProperty(nameof(WorkSession.AttendanceDate))!.SetValue(session,Date);
        return new MonthLockOvertimeDependency(row.Request,session,row.Approval,row.Decision);
    }).ToArray();
    public Guid Employee = Guid.NewGuid(), Site = Guid.NewGuid(), Team = Guid.NewGuid(), Policy = Guid.NewGuid(), EligibilityId = Guid.NewGuid();
    public DateOnly Date = new(2026, 10, 9);
    public DateTimeOffset Start = new(2026, 10, 9, 4, 0, 0, TimeSpan.Zero), ScheduleEnd = new(2026, 10, 9, 13, 0, 0, TimeSpan.Zero), End = new(2026, 10, 9, 15, 0, 0, TimeSpan.Zero);
    public AuthenticatedAccount Actor = new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 1, SessionClient.Web, true), Requester;
    public bool DurationReady = true, EligibilityReady = true, EligibilityAllowed = true, Locked, AssignmentValid = true, SessionCurrent = true, FailStorage, FailNotification, FixedSettingsAuthority = true;
    public string StaffCategory = "SITE";
    public List<OvertimeSessionSource> Sessions = []; public List<OvertimeRecord> Rows = []; public List<PermissionGrant> Grants = [];
    public List<IdempotencyReceipt> Receipts = []; public List<NotificationIntent> Notifications = [];
    public Dictionary<string, (string Purpose, byte[] Data)> Files = []; private readonly Dictionary<Guid, ApprovalState> saved = [];
    public AttendanceOperation Operation = null!;
    public IPrivateFileStore FileStore = null!;
    public OvertimeService Service; public AuthRequestContext Context = new("127.0.0.1", "synthetic-overtime");
    public OvertimeEligibilityService EligibilityService;
    public List<OvertimeEligibilityVersion> EligibilityRows = [];
    public OtClock Clock = new();
    public CompensationRecord Salary = null!;
    public string? RateRuleCode = OvertimeFormula.Version;
    public List<WorkTreatmentCreditEvidence> WorkCredits = [];
    public List<(Guid Employee, DateOnly From, DateOnly To)> WorkReads = [];
    public IWorkTreatmentCreditReader CreditReader = null!;
    public Func<IWorkTreatmentCreditReader?, OvertimeService> CreateService = null!;
    public IReadOnlyList<OvertimeEligibleInterval>? EligibleOverride;
    public Action<string>? AfterPrivateWrite;
    public bool InTransaction, FailWorkReader;
    public int CompensationWindowReads;
    public OtFixture(bool realRules = false, bool workCredits = false)
    {
        if (realRules) { Date = Date.AddDays(1); Start = Start.AddDays(1); ScheduleEnd = ScheduleEnd.AddDays(1); End = End.AddDays(1); }
        Requester = Actor; var shift = Guid.NewGuid();
        Sessions.Add(new(Guid.NewGuid(), Guid.NewGuid(), 1, Start, End, Site, shift, null, Guid.NewGuid(), Guid.NewGuid(), null, true));
        foreach (var permission in OvertimePermissions.Definitions) Grants.Add(new(permission.Code, permission.Sensitive, Guid.NewGuid(), AccessScopeKind.All, null));
        var current = OtProxy.Make<ICurrentAuthentication>((m, a) => Actor);
        var access = OtProxy.Make<IAccountAccessReader>((m, a) => Task.FromResult<IReadOnlyList<PermissionGrant>>(Grants.ToArray()));
        var authz = OtProxy.Make<IAccessManagementRepository>((m, a) => m.Name switch {
            "InTransactionAsync" => Transaction((Delegate)a[0]!, a[1]),
            "GetAssignmentRoleCodeAsync" => Task.FromResult<string?>(FixedSettingsAuthority && Grants.Any(x =>
                x.RoleAssignmentId == (Guid)a[0]! && x.Permission == OvertimePermissions.EligibilityManage) ? "ADMIN" : "Configured-HR"),
            "IsCurrentSystemRoleAssignmentAsync" => Task.FromResult(FixedSettingsAuthority && (Guid)a[0]! == Actor.AccountId &&
                Grants.Any(x => x.RoleAssignmentId == (Guid)a[1]! && x.Permission == OvertimePermissions.EligibilityManage)),
            _ => throw new Exception(m.Name) });
        var auth = OtProxy.Make<IAuthenticationRepository>((m, a) => throw new Exception(m.Name));
        var journal = OtProxy.Make<ISchedulingJournalRepository>((m, a) => m.Name switch {
            "IsCurrentSessionAsync" => Task.FromResult(SessionCurrent), "FindReceiptAsync" => Task.FromResult(Receipts.SingleOrDefault(x => x.ActorUserId == (string)a[0]! && x.Operation == (string)a[1]! && x.Key == (string)a[2]!)),
            "AddReceipt" => AddReceipt((IdempotencyReceipt)a[0]!), "AddAudit" => null, "SaveChangesAsync" => Task.CompletedTask, _ => throw new Exception(m.Name) });
        var files = OtProxy.Make<IPrivateFileStore>((m, a) => {
            if (m.Name == "WriteAsync") { if (FailStorage) throw new AuthenticationFailure("synthetic_storage_unavailable", 503); var bytes = (byte[])a[0]!; var id = Guid.NewGuid().ToString("N"); Files[id] = ((string)a[1]!, bytes); AfterPrivateWrite?.Invoke((string)a[1]!); return Task.FromResult(new PrivateFileReference(id, "1", Convert.ToHexString(SHA256.HashData(bytes)), "synthetic", bytes.Length)); }
            var value = Files[((PrivateFileReference)a[0]!).StorageKey]; if (value.Purpose != (string)a[1]!) throw new Exception("Wrong evidence purpose"); return Task.FromResult(value.Data); });
        var operation = new AttendanceOperation(current, access, authz, journal, auth, Clock, files);
        Operation = operation; FileStore = files;
        var assignment = Guid.NewGuid(); var category = Guid.NewGuid();
        var repo = OtProxy.Make<IOvertimeRepository>((m, a) => m.Name switch {
            "EmploymentAsync" => Task.FromResult<OvertimeEmploymentSource?>(new(assignment, category, StaffCategory, Site, Date.AddDays(-30), null)),
            "SessionSourcesAsync" => Task.FromResult<IReadOnlyList<OvertimeSessionSource>>(Sessions.ToArray()), "ApprovedPolicyAsync" => Task.FromResult(true),
            "FindAsync" => Task.FromResult(Rows.SingleOrDefault(x => x.Request.EmployeeId == (Guid)a[0]! && x.Request.Id == (Guid)a[1]!)),
            "CandidatesAsync" => Candidates((OvertimeQueueRequest)a[0]!, (AttendanceCursorPosition?)a[1], (int)a[2]!, (Guid?)a[3]),
            "HasConflictingRequestAsync" => Task.FromResult(Rows.Any(x => x.Request.EmployeeId == (Guid)a[0]! && x.Request.Id != (Guid?)a[3] && x.Request.StartsAtUtc < (DateTimeOffset)a[2]! && x.Request.EndsAtUtc > (DateTimeOffset)a[1]! && x.Approval.State is ApprovalState.Pending or ApprovalState.Approved)),
            "Add" => a.Length == 2 ? Add((OvertimeRequest)a[0]!, (ApprovalRequest)a[1]!) : AddDecision((ApprovalDecision)a[0]!),
            "ExpectVersion" => null, "SaveChangesAsync" => Save(), _ => throw new Exception(m.Name) });
        var summary = OtProxy.Make<IAttendanceSummaryRepository>((m, a) => m.Name switch {
            "ResourceScopesAsync" => Task.FromResult<IReadOnlyList<AttendanceResourceScope>>([new([new(EmployeeId: (Guid)a[0]!, SiteId: Site, TeamId: Team)])]),
            "LatestAsync" => Task.FromResult<Cosmix.Domain.Attendance.DailySummaries.DailyAttendanceSummary?>(null), "IsLockedAsync" => Task.FromResult(Locked), _ => throw new Exception(m.Name) });
        var rosters = OtProxy.Make<IIndividualRosterResolver>((m, a) => Task.FromResult(new EffectiveRosterResponse((Guid)a[0]!, (DateOnly)a[1]!, "scheduled", RosterSchedule.TimeZoneId,
            Site, assignment, Guid.Empty, shift, 1, Start, ScheduleEnd, 540, 0, 0)));
        var eligibilityRepository = OtProxy.Make<IOvertimeEligibilityRepository>((m, a) => m.Name switch {
            "TimelineAsync" => Task.FromResult<IReadOnlyList<OvertimeEligibilityVersion>>(EligibilityRows.Where(x => x.Target == (OvertimeEligibilityTarget)a[0]! && x.EmployeeId == (Guid?)a[1]).OrderBy(x => x.Revision).ToArray()),
            "Add" => AddEligibility((OvertimeEligibilityVersion)a[0]!), "SaveChangesAsync" => Task.CompletedTask, _ => throw new Exception(m.Name) });
        EligibilityService = new(operation, eligibilityRepository, repo, summary, files);
        var eligibility = OtProxy.Make<IOvertimeEligibilityReader>((m, a) => Task.FromResult(new OvertimeEligibility(EligibilityReady ? "configured" : "eligibility_not_configured", EligibilityReady ? EligibilityId : null, EligibilityReady ? EligibilityAllowed : null)));
        var policy = OtProxy.Make<IOvertimeDurationPolicy>((m, a) => Task.FromResult(DurationReady ? new OvertimeDurationResult("evaluated", Policy, "synthetic-confirmed-duration-v1", true, EligibleOverride ?? [new(ScheduleEnd, End)], RateRuleCode) : new OvertimeDurationResult("duration_policy_unavailable", null, null, null, [])));
        Salary = new CompensationRecord(Employee, 1, null, 100000, 120000, "PKR", WageBasis.Monthly, PaymentMethod.Bank, Date.AddDays(-30), null, null, "synthetic-approved-hiring", Start.AddDays(-30));
        var compensation = OtProxy.Make<IEmployeeCompensationRepository>((m, a) => m.Name switch { "HasConflictingHistoryAsync" => Task.FromResult(false), "WindowAsync" => ReadSalary(), _ => throw new Exception(m.Name) });
        var employment = OtProxy.Make<ICorrectionReviewRepository>((m, a) => m.Name switch { "HistoricallyEligibleAsync" => Task.FromResult(true), "PreservesAssignmentAsync" => Task.FromResult(AssignmentValid), "ScopeExistsAsync" => Task.FromResult(true), _ => throw new Exception(m.Name) });
        var source = new OvertimeSources(repo, summary, rosters, realRules ? EligibilityService : eligibility,
            realRules ? new ConfirmedOvertimeDurationPolicy() : policy, compensation, employment, operation);
        var tokens = new Dictionary<string, (Guid Account, string Purpose, string Filter, AttendanceCursorPosition Position)>();
        var cursors = OtProxy.Make<IAttendanceCursorProtector>((m, a) => { if (m.Name == "Protect") { var id = Guid.NewGuid().ToString(); tokens[id] = ((Guid)a[0]!, (string)a[1]!, (string)a[2]!, (AttendanceCursorPosition)a[3]!); return id; } var row = tokens[(string)a[0]!]; if (row.Account != (Guid)a[1]! || row.Purpose != (string)a[2]! || row.Filter != (string)a[3]!) throw new AuthenticationFailure("invalid_attendance_cursor", 400); return row.Position; });
        var notifications = OtProxy.Make<INotificationOutbox>((m, a) => { if (FailNotification) throw new AuthenticationFailure("synthetic_outbox_unavailable", 503); Notifications.Add((NotificationIntent)a[0]!); return Task.FromResult(true); });
        CreditReader = OtProxy.Make<IWorkTreatmentCreditReader>((m, a) => {
            if (m.Name != "ReadAsync") throw new Exception("OT must read actual proof, not invent work claims: " + m.Name);
            if (!InTransaction) throw new Exception("Work-credit proof escaped the authorized transaction");
            WorkReads.Add(((Guid)a[0]!, (DateOnly)a[1]!, (DateOnly)a[2]!));
            if (FailWorkReader) throw new AuthenticationFailure("work_treatment_credit_source_changed", 409);
            return Task.FromResult<IReadOnlyList<WorkTreatmentCreditEvidence>>(WorkCredits.ToArray());
        });
        var constructor = typeof(OvertimeService).GetConstructors().Single();
        object?[] arguments = [operation, repo, source, summary, employment, files, access, cursors, notifications];
        var recoveryReader = OtProxy.Make<ILeaveRecoveryCreditReader>((m, a) => {
            if (m.Name != "ReadReservedAsync" || !InTransaction) throw new Exception("Recovery allocations require the authorized transaction");
            return Task.FromResult<IReadOnlyList<WorkTreatmentCreditEvidence>>([]);
        });
        CreateService = reader => (OvertimeService)constructor.Invoke(constructor.GetParameters()
            .Any(x => x.ParameterType == typeof(ILeaveRecoveryCreditReader)) ? [.. arguments, reader, recoveryReader] :
            constructor.GetParameters().Any(x => x.ParameterType == typeof(IWorkTreatmentCreditReader)) ? [.. arguments, reader] : arguments);
        Service = CreateService(workCredits ? CreditReader : null);
    }
    public async Task<SubmitOvertimeRequest> Proposal() => MakeProposal((await Service.SourcesAsync(Employee, Date, false, Context, default)).SourceFingerprint);
    public SubmitOvertimeRequest MakeProposal(string fingerprint) => new(Date, Sessions[0].Id, ScheduleEnd, End, OvertimeActivity.Work, fingerprint, "Documented assigned work", "Synthetic written work evidence", "job-001");
    public Task<OvertimeResponse> Submit(SubmitOvertimeRequest request) => Service.SubmitAsync(Employee, request, "overtime-submit-001", false, Context, default);
    public Task<OvertimeResponse> Decide(OvertimeResponse row, ApprovalState decision) => Service.DecideAsync(Employee, row.Id, new(row.Version, decision, "Reviewed synthetic evidence"), "overtime-decision-" + decision, Context, default);
    public void Reviewer() => Actor = Actor with { AccountId = Guid.NewGuid(), EmployeeId = Guid.NewGuid(), Client = SessionClient.Web, MfaSatisfied = true };
    public void RestrictToTeam(Guid team) { Grants.Clear(); foreach (var p in OvertimePermissions.Definitions.Where(x => !x.SelfService)) Grants.Add(new(p.Code, p.Sensitive, Guid.NewGuid(), AccessScopeKind.Team, team)); }
    public WorkTreatmentCreditEvidence Credit(WorkCreditRole role, DateTimeOffset from, DateTimeOffset to) {
        var id = Guid.NewGuid();
        return new(id, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), new string('C', 64), new string('D', 64),
            new(Employee, id, role, Guid.NewGuid(), Guid.NewGuid(), Date, from, to), null) {
            PolicyVersionId = Policy, OriginalScopes = [new([new(EmployeeId: Employee, SiteId: Site, TeamId: Team)])]
        };
    }
    private Task<CompensationWindow> ReadSalary() { CompensationWindowReads++; return Task.FromResult(new CompensationWindow(Salary, null, Salary)); }
    private object? Transaction(Delegate action, object? token) {
        var prior = InTransaction; InTransaction = true;
        try {
            var result = action.DynamicInvoke(token);
            if (result is Task task) _ = task.ContinueWith(_ => InTransaction = prior, TaskContinuationOptions.ExecuteSynchronously);
            else InTransaction = prior;
            return result;
        } catch { InTransaction = prior; throw; }
    }
    private object? AddReceipt(IdempotencyReceipt item) { Receipts.Add(item); return null; }
    private object? AddEligibility(OvertimeEligibilityVersion item) { EligibilityRows.Add(item); return null; }
    private object? Add(OvertimeRequest row, ApprovalRequest approval) { Rows.Add(new() { Request = row, Approval = approval, BusinessDate = Date }); return null; }
    private object? AddDecision(ApprovalDecision decision) { var i = Rows.FindIndex(x => x.Approval.Id == decision.ApprovalRequestId); Rows[i] = Rows[i] with { Decision = decision }; return null; }
    private Task Save() { foreach (var row in Rows) { if (!saved.TryGetValue(row.Approval.Id, out var state) || state != row.Approval.State) { var v = row.Approval.RowVersion.Length == 8 ? BitConverter.ToInt64(row.Approval.RowVersion) : 0; typeof(ApprovalRequest).GetProperty("RowVersion")!.SetValue(row.Approval, BitConverter.GetBytes(v + 1)); saved[row.Approval.Id] = row.Approval.State; } } return Task.CompletedTask; }
    private Task<IReadOnlyList<OvertimeRecord>> Candidates(OvertimeQueueRequest request, AttendanceCursorPosition? after, int limit, Guid? requester)
    {
        var query = Rows.Where(x => x.BusinessDate >= request.From && x.BusinessDate < request.To && (!request.State.HasValue || request.State == x.Approval.State) &&
            (!requester.HasValue || x.Request.RequestedBy == requester.ToString()) && (request.Scope != CorrectionQueueScope.Employee || request.TargetId == x.Request.EmployeeId));
        if (after is not null) query = query.Where(x => x.Request.RequestedAtUtc > after.CreatedAtUtc || x.Request.RequestedAtUtc == after.CreatedAtUtc && x.Request.Id.CompareTo(after.Id) > 0);
        return Task.FromResult<IReadOnlyList<OvertimeRecord>>(query.OrderBy(x => x.Request.RequestedAtUtc).ThenBy(x => x.Request.Id).Take(limit).ToArray());
    }
}
