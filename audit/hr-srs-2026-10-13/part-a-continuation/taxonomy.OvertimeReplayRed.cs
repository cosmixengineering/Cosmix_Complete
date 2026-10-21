using System.Reflection;
using System.Text.Json;
using Cosmix.Application.HR.StaffCategories;
using System.Security.Cryptography;
using Cosmix.Application.Attendance.Common;
using Cosmix.Application.Attendance.Corrections;
using Cosmix.Application.Attendance.DailySummaries;
using Cosmix.Application.Attendance.Overtime;
using Cosmix.Application.Attendance.Rosters;
using Cosmix.Application.Attendance.Shifts;
using Cosmix.Application.Authentication;
using Cosmix.Application.Authentication.Access;
using Cosmix.Application.Common.Files;
using Cosmix.Application.Common.Notifications;
using Cosmix.Application.HR.Compensation;
using Cosmix.Domain.Attendance.Overtime;
using Cosmix.Domain.Authentication;
using Cosmix.Domain.Common.Approvals;
using Cosmix.Domain.Common.Idempotency;
using Cosmix.Domain.HR.Compensation;


var fixture = new OtFixture(false,true);
var proposal = await fixture.Proposal(); await fixture.Submit(proposal);
fixture.ReturnBaseline=true;
try { await fixture.Submit(proposal); throw new Exception("Published SOURCE replay escaped through null current baseline proof"); }
catch (AuthenticationFailure e) when (e.Code == "overtime_source_changed") { Console.WriteLine("PASS published SOURCE replay denies null current baseline proof"); }
public sealed class OtClock : TimeProvider { public override DateTimeOffset GetUtcNow() => new(2026, 10, 10, 16, 0, 0, TimeSpan.Zero); }
public class OtProxy : DispatchProxy
{
    public Func<MethodInfo, object?[], object?> Handler = null!;
    protected override object? Invoke(MethodInfo? method, object?[]? args) => Handler(method!, args ?? []);
    public static T Make<T>(Func<MethodInfo, object?[], object?> handler) where T : class
    { var proxy = Create<T, OtProxy>(); ((OtProxy)(object)proxy).Handler = handler; return proxy; }
}
public sealed class OtFixture
{
    public Guid Employee = Guid.NewGuid(), Site = Guid.NewGuid(), Team = Guid.NewGuid(), Policy = Guid.NewGuid(), EligibilityId = Guid.NewGuid();
    public DateOnly Date = new(2026, 10, 9);
    public DateTimeOffset Start = new(2026, 10, 9, 4, 0, 0, TimeSpan.Zero), ScheduleEnd = new(2026, 10, 9, 13, 0, 0, TimeSpan.Zero), End = new(2026, 10, 9, 15, 0, 0, TimeSpan.Zero);
    public AuthenticatedAccount Actor = new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 1, SessionClient.Web, true), Requester;
    public bool DurationReady = true, EligibilityReady = true, EligibilityAllowed = true, Locked, AssignmentValid = true, SessionCurrent = true, FailStorage, FailNotification, FixedSettingsAuthority = true;
    public string StaffCategory = "SITE";
    public bool ReturnBaseline, TaxonomyMode, MappingAvailable = true, OutsideInheritance = true, OfficeMode, RangeUnavailable, RosterWrongCode, WrongMembership;
    public Guid TaxonomyVersion = Guid.NewGuid(), OfficeBranch = Guid.NewGuid();
    public string TaxonomyHash = new('A', 64);
    public Action<string>? OnPrivateWrite;
    public OvertimeSources Sources;
    public StaffTaxonomyResolution Mapping() => !TaxonomyMode || ReturnBaseline ? StaffTaxonomyResolver.Resolve(StaffCategory, Date, []) :
        !MappingAvailable ? new("taxonomy_not_effective", null, null, new('F', 64)) :
        new("effective", new(StaffCategory, "Synthetic outside category", OfficeMode ? StaffWorkLocation.Office : StaffWorkLocation.Site,
            OfficeMode ? StaffScheduleBasis.Office : StaffScheduleBasis.Site, false, OutsideInheritance, []), TaxonomyVersion, TaxonomyHash,
            false, Date.AddDays(-30), null);
    public RosterStaffTaxonomySource? Pin() => !TaxonomyMode || ReturnBaseline || !MappingAvailable ? null : new(RosterWrongCode ? "OTHER" : StaffCategory,
        TaxonomyVersion, TaxonomyHash, OfficeMode ? StaffWorkLocation.Office : StaffWorkLocation.Site,
        OfficeMode ? StaffScheduleBasis.Office : StaffScheduleBasis.Site, OutsideInheritance);
    public List<OvertimeSessionSource> Sessions = []; public List<OvertimeRecord> Rows = []; public List<PermissionGrant> Grants = [];
    public List<IdempotencyReceipt> Receipts = []; public List<NotificationIntent> Notifications = [];
    public Dictionary<string, (string Purpose, byte[] Data)> Files = []; private readonly Dictionary<Guid, ApprovalState> saved = [];
    public OvertimeService Service; public AuthRequestContext Context = new("127.0.0.1", "synthetic-overtime");
    public OvertimeEligibilityService EligibilityService;
    public List<OvertimeEligibilityVersion> EligibilityRows = [];
    public OtFixture(bool realRules = false, bool taxonomyMode = false)
    {
        if (realRules) { Date = Date.AddDays(1); Start = Start.AddDays(1); ScheduleEnd = ScheduleEnd.AddDays(1); End = End.AddDays(1); }
        TaxonomyMode = taxonomyMode; Requester = Actor; var shift = Guid.NewGuid();
        Sessions.Add(new(Guid.NewGuid(), Guid.NewGuid(), 1, Start, End, Site, shift, null, Guid.NewGuid(), Guid.NewGuid(), null, true));
        foreach (var permission in OvertimePermissions.Definitions) Grants.Add(new(permission.Code, permission.Sensitive, Guid.NewGuid(), AccessScopeKind.All, null));
        var current = OtProxy.Make<ICurrentAuthentication>((m, a) => Actor);
        var access = OtProxy.Make<IAccountAccessReader>((m, a) => Task.FromResult<IReadOnlyList<PermissionGrant>>(Grants.ToArray()));
        var authz = OtProxy.Make<IAccessManagementRepository>((m, a) => m.Name switch {
            "InTransactionAsync" => ((Delegate)a[0]!).DynamicInvoke(a[1]),
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
            if (m.Name == "WriteAsync") { if (FailStorage) throw new AuthenticationFailure("synthetic_storage_unavailable", 503); var bytes = (byte[])a[0]!; var id = Guid.NewGuid().ToString("N"); Files[id] = ((string)a[1]!, bytes); OnPrivateWrite?.Invoke((string)a[1]!); return Task.FromResult(new PrivateFileReference(id, "1", Convert.ToHexString(SHA256.HashData(bytes)), "synthetic", bytes.Length)); }
            var value = Files[((PrivateFileReference)a[0]!).StorageKey]; if (value.Purpose != (string)a[1]!) throw new Exception("Wrong evidence purpose"); return Task.FromResult(value.Data); });
        var operation = new AttendanceOperation(current, access, authz, journal, auth, new OtClock(), files);
        var assignment = Guid.NewGuid(); var category = Guid.NewGuid();
        var repo = OtProxy.Make<IOvertimeRepository>((m, a) => m.Name switch {
            "EmploymentAsync" => Task.FromResult<OvertimeEmploymentSource?>(new(assignment, category, StaffCategory, OfficeMode ? null : Site, Date.AddDays(-30), null, OfficeMode ? OfficeBranch : null)),
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
            OfficeMode ? null : Site, assignment, Guid.Empty, shift, 1, Start, ScheduleEnd, 540, 0, 0) { Taxonomy = Pin(), BranchId = OfficeMode ? OfficeBranch : null, Explanation = taxonomyMode ? new("synthetic-taxonomy", null, WrongMembership ? Guid.NewGuid() : assignment, null, null, "synthetic") : null }));
        var eligibilityRepository = OtProxy.Make<IOvertimeEligibilityRepository>((m, a) => m.Name switch {
            "TimelineAsync" => Task.FromResult<IReadOnlyList<OvertimeEligibilityVersion>>(EligibilityRows.Where(x => x.Target == (OvertimeEligibilityTarget)a[0]! && x.EmployeeId == (Guid?)a[1]).OrderBy(x => x.Revision).ToArray()),
            "Add" => AddEligibility((OvertimeEligibilityVersion)a[0]!), "SaveChangesAsync" => Task.CompletedTask, _ => throw new Exception(m.Name) });
        var taxonomy = OtProxy.Make<IStaffTaxonomyResolver>((m, a) => Task.FromResult(m.Name == "ResolveForRangeAsync" && RangeUnavailable
            ? new StaffTaxonomyResolution("mapping_changes_during_interval", null, null, new('F', 64)) : Mapping()));
        EligibilityService = new(operation, eligibilityRepository, repo, summary, files, taxonomyMode ? taxonomy : null);
        var eligibility = OtProxy.Make<IOvertimeEligibilityReader>((m, a) => Task.FromResult(new OvertimeEligibility(EligibilityReady ? "configured" : "eligibility_not_configured", EligibilityReady ? EligibilityId : null, EligibilityReady ? EligibilityAllowed : null)));
        var policy = OtProxy.Make<IOvertimeDurationPolicy>((m, a) => Task.FromResult(DurationReady ? new OvertimeDurationResult("evaluated", Policy, "synthetic-confirmed-duration-v1", true, [new(ScheduleEnd, End)]) : new OvertimeDurationResult("duration_policy_unavailable", null, null, null, [])));
        var salary = new CompensationRecord(Employee, 1, null, 100000, 120000, "PKR", WageBasis.Monthly, PaymentMethod.Bank, Date.AddDays(-30), null, null, "synthetic-approved-hiring", Start.AddDays(-30));
        var compensation = OtProxy.Make<IEmployeeCompensationRepository>((m, a) => m.Name switch { "HasConflictingHistoryAsync" => Task.FromResult(false), "WindowAsync" => Task.FromResult(new CompensationWindow(salary, null, salary)), _ => throw new Exception(m.Name) });
        var employment = OtProxy.Make<ICorrectionReviewRepository>((m, a) => m.Name switch { "HistoricallyEligibleAsync" => Task.FromResult(true), "PreservesAssignmentAsync" => Task.FromResult(AssignmentValid), "ScopeExistsAsync" => Task.FromResult(true), _ => throw new Exception(m.Name) });
        var source = new OvertimeSources(repo, summary, rosters, realRules ? EligibilityService : eligibility,
            realRules ? new ConfirmedOvertimeDurationPolicy() : policy, compensation, employment, operation, taxonomyMode ? taxonomy : null); Sources = source;
        var tokens = new Dictionary<string, (Guid Account, string Purpose, string Filter, AttendanceCursorPosition Position)>();
        var cursors = OtProxy.Make<IAttendanceCursorProtector>((m, a) => { if (m.Name == "Protect") { var id = Guid.NewGuid().ToString(); tokens[id] = ((Guid)a[0]!, (string)a[1]!, (string)a[2]!, (AttendanceCursorPosition)a[3]!); return id; } var row = tokens[(string)a[0]!]; if (row.Account != (Guid)a[1]! || row.Purpose != (string)a[2]! || row.Filter != (string)a[3]!) throw new AuthenticationFailure("invalid_attendance_cursor", 400); return row.Position; });
        var notifications = OtProxy.Make<INotificationOutbox>((m, a) => { if (FailNotification) throw new AuthenticationFailure("synthetic_outbox_unavailable", 503); Notifications.Add((NotificationIntent)a[0]!); return Task.FromResult(true); });
        Service = new(operation, repo, source, summary, employment, files, access, cursors, notifications);
    }
    public async Task<SubmitOvertimeRequest> Proposal() => MakeProposal((await Service.SourcesAsync(Employee, Date, false, Context, default)).SourceFingerprint);
    public SubmitOvertimeRequest MakeProposal(string fingerprint) => new(Date, Sessions[0].Id, ScheduleEnd, End, OvertimeActivity.Work, fingerprint, "Documented assigned work", "Synthetic written work evidence", "job-001");
    public Task<OvertimeResponse> Submit(SubmitOvertimeRequest request) => Service.SubmitAsync(Employee, request, "overtime-submit-001", false, Context, default);
    public Task<OvertimeResponse> Decide(OvertimeResponse row, ApprovalState decision) => Service.DecideAsync(Employee, row.Id, new(row.Version, decision, "Reviewed synthetic evidence"), "overtime-decision-" + decision, Context, default);
    public void Reviewer() => Actor = Actor with { AccountId = Guid.NewGuid(), EmployeeId = Guid.NewGuid(), Client = SessionClient.Web, MfaSatisfied = true };
    public void RestrictToTeam(Guid team) { Grants.Clear(); foreach (var p in OvertimePermissions.Definitions.Where(x => !x.SelfService)) Grants.Add(new(p.Code, p.Sensitive, Guid.NewGuid(), AccessScopeKind.Team, team)); }
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
