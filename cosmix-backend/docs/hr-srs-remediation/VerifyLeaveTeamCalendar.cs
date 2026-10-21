using System.Reflection;
using System.Text.Json;
using Cosmix.Application.Attendance.Common;
using Cosmix.Application.Attendance.Rosters;
using Cosmix.Application.Attendance.Shifts;
using Cosmix.Application.Authentication;
using Cosmix.Application.Authentication.Access;
using Cosmix.Application.Common.Files;
using Cosmix.Application.HR.Leaves;
using Cosmix.Domain.Authentication;
using Cosmix.Domain.Common.Approvals;
using Cosmix.Domain.Common.Audit;
using Cosmix.Domain.HR.Leaves;

// Run alone through VerifyManaged.ps1 against the parent's integrated build. No SQL or providers.
if (AccessPermissionCatalog.Find(LeaveTeamCalendarPermissions.Read) is null)
    throw new Exception("Parent wiring missing: add LeaveTeamCalendarPermissions.Definitions to AccessPermissionCatalog.");
var failures = new List<string>();
var checks = 0;
void Check(bool condition, string name) { checks++; if (!condition) failures.Add(name); }
async Task Fails(Func<Task> run, int status, string name)
{
    try { await run(); Check(false, name); }
    catch (AuthenticationFailure e) { Check(e.StatusCode == status, name + ": " + e.Code); }
}
var f = new CalendarFixture();
var first = f.NewLeave(f.Employee, [new(2026, 10, 1), new(2026, 10, 3)], ApprovalState.Approved);
f.Rows.Add(first);
var page = await f.Team();
Check(page.TotalCount == 2 && page.Items.Select(x => x.Date).SequenceEqual(new[] { new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 3) }),
    "sparse requested days never fabricate an intervening leave day");
Check(page.Items.All(x => x.EmployeeId == f.Employee && x.Status == "approved"), "team member reads approved status");
using (var json = JsonDocument.Parse(JsonSerializer.Serialize(page)))
    Check(json.RootElement.GetProperty("Items").EnumerateArray().All(x => x.EnumerateObject().Select(p => p.Name)
        .Order().SequenceEqual(new[] { "Date", "EmployeeId", "Status" })), "calendar items expose exactly date, employee and status");
Check(!JsonSerializer.Serialize(page).Contains("PRIVATE", StringComparison.Ordinal), "no private reason, evidence, contact or type reaches DTO");
Check(f.Audits.Count == 1 && f.Saves == 1, "successful calendar read persists access audit");
Check(f.ScopeReads.Any(x => x.From == new DateOnly(2026, 10, 19) && x.To == new DateOnly(2026, 10, 20)) &&
    f.ScopeReads.Any(x => x.From == new DateOnly(2026, 10, 1) && x.To == new DateOnly(2026, 11, 1)),
    "current Karachi day and inclusive historical range both use end-exclusive roster intervals");
page = await f.Team(page: 2, pageSize: 1);
Check(page.TotalCount == 2 && page.Items.Single().Date == new DateOnly(2026, 10, 3), "stable pagination follows date then employee and status");
page = await f.Team(from: new(2026, 10, 3), to: new(2026, 10, 3));
Check(page.Items.Count == 1 && page.Items[0].Date == new DateOnly(2026, 10, 3), "one-day range includes its endpoint only");
f.Rows.Add(f.NewLeave(f.Employee, [new(2026, 10, 3)], ApprovalState.Approved));
Check((await f.Team()).TotalCount == 2, "duplicate same-date status does not expose application counts");

f = new(); f.Rows.Add(f.NewLeave(f.Employee, [new(2026, 10, 2)], ApprovalState.Approved));
f.CurrentTeam = Guid.NewGuid();
await Fails(() => f.Team(), 403, "old team authority cannot reveal a transferred employee");
Check(f.LeaveReads == 0, "current scope denied before leave records load");
f.CurrentTeam = f.TeamId; f.HistoricalTeam = Guid.NewGuid();
await Fails(() => f.Team(), 403, "current team authority does not unlock another historical team");
Check(f.LeaveReads == 0, "historical scope denied before leave records load");
f.HistoricalTeam = f.TeamId; f.IncludeUnpermittedHistoricalSlice = true;
await Fails(() => f.Team(), 403, "every historical slice requires authority, not just one matching slice");
f.IncludeUnpermittedHistoricalSlice = false; f.IncludeUnpermittedAlternative = true;
Check((await f.Team()).TotalCount == 1, "one permitted membership alternative per slice suffices");
f.IncludeUnpermittedAlternative = false;
await Fails(() => f.Team(ids: [f.Employee, Guid.NewGuid()]), 403, "mixed authorized and unauthorized selection is all-or-nothing");
var before = f.LeaveReads;
f.EmptyScopes = true;
await Fails(() => f.Team(), 409, "empty scope evidence fails closed");
Check(f.LeaveReads == before, "missing scope evidence is not an empty successful calendar");
f.EmptyScopes = false; f.WrongScopeEmployee = true;
await Fails(() => f.Team(), 409, "scope evidence for another employee cannot authorize selected employee");
f.WrongScopeEmployee = false; f.TooManyScopes = true;
await Fails(() => f.Team(), 409, "scope traversal remains bounded");

f = new(); f.Grants.Clear(); f.Grant(AccessScopeKind.Site, f.SiteId);
Check((await f.Team()).TotalCount == 0, "site grant authorizes an empty scoped calendar without review permission");
f.Grants.Clear(); f.Grant(AccessScopeKind.All, null);
Check((await f.Team()).TotalCount == 0, "HR all-scope read is supported without medical review permission");
f.Identity = f.Identity with { MfaSatisfied = false };
await Fails(() => f.Team(), 403, "team calendar requires web MFA");
f.Identity = f.Identity with { MfaSatisfied = true, Client = SessionClient.Mobile };
await Fails(() => f.Team(), 403, "mobile self session cannot use team endpoint");
f = new(); f.Grants.Clear(); f.Grant(AccessScopeKind.Self, f.Identity.EmployeeId);
await Fails(() => f.Team(), 403, "Self-scoped management grant cannot read another employee");
Check((await f.Team(ids: [f.Identity.EmployeeId])).TotalCount == 0, "Self-scoped management grant remains own employee only");
f = new(); f.Grants.Clear(); f.Grant(AccessScopeKind.Team, f.TeamId, effectiveTo: f.Clock.Now);
await Fails(() => f.Team(), 403, "expired grant cannot read team leave");
Check(f.LeaveReads == 0, "expiry is checked before leave IO");

f = new(); f.Identity = f.Identity with { Client = SessionClient.Mobile, MfaSatisfied = false };
f.Grants.Clear(); f.Grants.Add(new(LeavePermissions.Self, false, Guid.NewGuid(), AccessScopeKind.All, null)
    { AuthorityTimingKnown = true, ValidFromUtc = f.Clock.Now.AddDays(-1) });
f.Rows.Add(f.NewLeave(f.Identity.EmployeeId, [new(2026, 10, 4)], ApprovalState.Pending));
f.Rows.Add(f.NewLeave(f.Employee, [new(2026, 10, 4)], ApprovalState.Approved));
page = await f.Service.MineAsync(new(new(2026, 10, 1), new(2026, 10, 31)), f.Context, default);
Check(page.Items.Count == 1 && page.Items[0].EmployeeId == f.Identity.EmployeeId && page.Items[0].Status == "pending",
    "self calendar derives identity from session even for a broad stored self-service grant");
Check(f.ScopeReads.Count == 0 && f.ReadEmployees.SequenceEqual(new[] { f.Identity.EmployeeId }), "self read never enumerates team or foreign leave");
f = new(); f.AfterRead = () => f.SessionCurrent = false;
await Fails(() => f.Team(), 401, "session revocation during leave IO prevents disclosure");
f = new(); f.AfterRead = () => f.Grants.Clear();
await Fails(() => f.Team(), 403, "permission removal during leave IO prevents disclosure");
f = new(); f.AfterRead = () => f.CurrentTeam = Guid.NewGuid();
await Fails(() => f.Team(), 403, "current transfer during leave IO prevents disclosure");
f = new(); f.AfterRead = () => f.HistoricalTeam = Guid.NewGuid();
await Fails(() => f.Team(), 403, "historical scope change during leave IO prevents disclosure");
f = new(); f.FailAudit = true;
await Fails(() => f.Team(), 409, "failed access audit prevents response");

f = new();
foreach (var state in new[] { ApprovalState.Pending, ApprovalState.Approved, ApprovalState.Returned, ApprovalState.Rejected, ApprovalState.Cancelled })
    f.Rows.Add(f.NewLeave(f.Employee, [new(2026, 10, 5 + (int)state)], state));
Check((await f.Team()).Items.Select(x => x.Status).SequenceEqual(new[] { "pending", "approved", "returned", "rejected", "cancelled" }),
    "retained approval states project without invented day or pay treatment");
f = new(); var approved = f.NewLeave(f.Employee, [new(2026, 10, 7)], ApprovalState.Approved);
f.Rows.Add(f.WithCancellation(approved, ApprovalState.Pending));
Check((await f.Team()).Items.Single().Status == "cancellation_pending", "pending cancellation never masquerades as completed cancellation");
f.Rows[0] = f.WithCancellation(approved, ApprovalState.Approved);
Check((await f.Team()).Items.Single().Status == "cancelled", "approved cancellation changes calendar state");
f.Rows[0] = f.WithCancellation(approved, ApprovalState.Rejected);
Check((await f.Team()).Items.Single().Status == "approved", "rejected cancellation leaves approved leave intact");
f.Rows[0] = f.NewLeave(f.Employee, [new(2026, 10, 7)], ApprovalState.Returned) with { SupersededByRequestId = Guid.NewGuid() };
Check((await f.Team()).Items.Single().Status == "superseded", "returned revision does not masquerade as current approval");
f.Rows[0] = approved with { SupersededByRequestId = Guid.NewGuid() };
await Fails(() => f.Team(), 409, "impossible superseded approval is an evidence conflict");
f.Rows[0] = approved with { Decisions = [] };
await Fails(() => f.Team(), 409, "approved status requires its matching decision");
f.Rows[0] = approved with { Approval = f.NewLeave(f.Employee, [new(2026, 10, 7)], ApprovalState.Approved).Approval };
await Fails(() => f.Team(), 409, "foreign approval cannot supply status");
f.Rows[0] = f.WithCancellation(approved, ApprovalState.Approved);
f.Rows[0] = f.Rows[0] with { Cancellations = [f.Rows[0].Cancellations[0] with { Decisions = [] }] };
await Fails(() => f.Team(), 409, "cancellation without decision cannot suppress approved leave");
f.Rows[0] = approved;
approved.Request.Days.Clear();
await Fails(() => f.Team(), 409, "missing exact days never falls back to the request envelope");
f = new(); var sparse = f.NewLeave(f.Employee, [new(2026, 10, 1), new(2026, 10, 3)], ApprovalState.Pending);
f.Rows.Add(sparse);
sparse.Request.Days.Add(sparse.Request.Days.First());
await Fails(() => f.Team(), 409, "duplicate allocation is rejected before projection deduplication");
f = new(); f.ReturnWrongEmployee = true; f.Rows.Add(f.NewLeave(Guid.NewGuid(), [new(2026, 10, 1)], ApprovalState.Pending));
await Fails(() => f.Team(), 409, "repository contamination cannot leak foreign leave");
f = new(); for (var i = 0; i < 1001; i++) f.Rows.Add(f.NewLeave(f.Employee, [new(2026, 10, 1)], ApprovalState.Pending));
await Fails(() => f.Team(), 409, "aggregate source history remains bounded");

f = new();
await Fails(() => f.Team(ids: []), 400, "empty selection cannot become all employees");
await Fails(() => f.Team(ids: [Guid.Empty]), 400, "empty employee identity is rejected");
await Fails(() => f.Team(ids: [f.Employee, f.Employee]), 400, "duplicate employees do not bypass source limits");
await Fails(() => f.Team(ids: Enumerable.Range(0, 26).Select(_ => Guid.NewGuid()).ToArray()), 400, "employee selection is bounded");
await Fails(() => f.Team(to: new(2026, 11, 1)), 400, "32 inclusive days exceeds the bounded calendar window");
await Fails(() => f.Team(from: new(2026, 11, 1)), 400, "reversed range is rejected");
await Fails(() => f.Team(page: 0), 400, "invalid page is rejected");
await Fails(() => f.Team(pageSize: 101), 400, "oversize page is rejected");
Check(f.LeaveReads == 0 && f.ScopeReads.Count == 0, "invalid bounds perform no scope or leave IO");
var selection = new[] { f.Employee };
f.OnScopeRead = () => selection[0] = Guid.NewGuid();
await f.Team(ids: selection);
Check(f.ReadEmployees.Single() == f.Employee, "caller array mutation cannot change the authorized selection");
foreach (var failure in failures) Console.Error.WriteLine("FAIL: " + failure);
Console.WriteLine($"Leave team calendar: {checks - failures.Count}/{checks}; actual service/authorization with synthetic IO, no SQL or HTTP acceptance.");
return failures.Count == 0 ? 0 : 1;

public sealed class CalendarClock : TimeProvider
{
    public DateTimeOffset Now = new(2026, 10, 18, 20, 0, 0, TimeSpan.Zero);
    public override DateTimeOffset GetUtcNow() => Now;
}
public class CalendarProxy : DispatchProxy
{
    public Func<MethodInfo, object?[], object?> Handler = null!;
    protected override object? Invoke(MethodInfo? method, object?[]? args) => Handler(method!, args ?? []);
    public static T Make<T>(Func<MethodInfo, object?[], object?> handler) where T : class
    { var result = Create<T, CalendarProxy>(); ((CalendarProxy)(object)result).Handler = handler; return result; }
}
public sealed class CalendarFixture
{
    public CalendarClock Clock = new();
    public AuthenticatedAccount Identity = new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 1, SessionClient.Web, true);
    public Guid Employee = Guid.NewGuid(), TeamId = Guid.NewGuid(), SiteId = Guid.NewGuid(), CurrentTeam, HistoricalTeam;
    public List<PermissionGrant> Grants = [];
    public List<LeaveRow> Rows = [];
    public List<AuditEvent> Audits = [];
    public List<Guid> ReadEmployees = [];
    public List<(DateOnly From, DateOnly? To)> ScopeReads = [];
    public bool SessionCurrent = true, FailAudit, EmptyScopes, WrongScopeEmployee, TooManyScopes,
        IncludeUnpermittedHistoricalSlice, IncludeUnpermittedAlternative, ReturnWrongEmployee;
    public int LeaveReads, Saves;
    public Action? AfterRead, OnScopeRead;
    public LeaveTeamCalendarService Service;
    public AuthRequestContext Context = new("127.0.0.1", "synthetic-leave-calendar");
    public CalendarFixture()
    {
        CurrentTeam = HistoricalTeam = TeamId;
        Grant(AccessScopeKind.Team, TeamId);
        var current = CalendarProxy.Make<ICurrentAuthentication>((_, _) => Identity);
        var access = CalendarProxy.Make<IAccountAccessReader>((_, _) => Task.FromResult<IReadOnlyList<PermissionGrant>>(Grants.ToArray()));
        var auth = CalendarProxy.Make<IAccessManagementRepository>((m, a) => m.Name switch
        {
            "InTransactionAsync" => ((Delegate)a[0]!).DynamicInvoke(a[1]),
            "GetAssignmentRoleCodeAsync" => Task.FromResult<string?>("Supervisor"),
            _ => throw new Exception("Unexpected authorization IO: " + m.Name)
        });
        var journal = CalendarProxy.Make<ISchedulingJournalRepository>((m, a) =>
        {
            if (m.Name == "IsCurrentSessionAsync") return Task.FromResult(SessionCurrent);
            if (m.Name == "AddAudit") { Audits.Add((AuditEvent)a[0]!); return null; }
            if (m.Name == "SaveChangesAsync") { Saves++; return FailAudit ? Task.FromException(new AuthenticationFailure("synthetic_audit_failure", 409)) : Task.CompletedTask; }
            throw new Exception("Unexpected journal IO: " + m.Name);
        });
        var files = CalendarProxy.Make<IPrivateFileStore>((m, _) => throw new Exception("Private file access forbidden: " + m.Name));
        var operation = new AttendanceOperation(current, access, auth, journal,
            CalendarProxy.Make<IAuthenticationRepository>((m, _) => throw new Exception(m.Name)), Clock, files);
        var leaves = CalendarProxy.Make<ILeaveRepository>((m, a) =>
        {
            if (m.Name == "EmployeeExistsAsync") return Task.FromResult(true);
            if (m.Name != "OverlappingAsync") throw new Exception("Unexpected leave IO: " + m.Name);
            LeaveReads++; var employee = (Guid)a[0]!; var from = (DateOnly)a[1]!; var to = (DateOnly)a[2]!;
            ReadEmployees.Add(employee);
            var rows = Rows.Where(x => (ReturnWrongEmployee || x.Request.EmployeeId == employee) &&
                x.Request.StartDate <= to && x.Request.EndDate >= from).ToArray();
            AfterRead?.Invoke();
            return Task.FromResult<IReadOnlyList<LeaveRow>>(rows);
        });
        var rosters = CalendarProxy.Make<IRosterRepository>((m, a) =>
        {
            if (m.Name != "ResourcesAsync") throw new Exception("Unexpected roster IO: " + m.Name);
            var employee = (Guid)a[0]!; var from = (DateOnly)a[1]!;
            ScopeReads.Add((from, (DateOnly?)a[2])); OnScopeRead?.Invoke();
            var team = employee == Employee || employee == Identity.EmployeeId
                ? from == new DateOnly(2026, 10, 19) ? CurrentTeam : HistoricalTeam : Guid.NewGuid();
            List<AccessResource> alternatives = [new(EmployeeId: WrongScopeEmployee ? Guid.NewGuid() : employee, TeamId: team, SiteId: SiteId)];
            if (IncludeUnpermittedAlternative) alternatives.Add(new(EmployeeId: employee, TeamId: Guid.NewGuid()));
            List<SchedulingResourceScope> scopes = [new(alternatives)];
            if (IncludeUnpermittedHistoricalSlice && from != new DateOnly(2026, 10, 19)) scopes.Add(new([new(EmployeeId: employee, TeamId: Guid.NewGuid())]));
            if (TooManyScopes) scopes = Enumerable.Range(0, 4097).Select(_ => new SchedulingResourceScope(alternatives)).ToList();
            return Task.FromResult<IReadOnlyList<SchedulingResourceScope>>(EmptyScopes ? [] : scopes);
        });
        Service = new(operation, leaves, rosters, access, journal, Clock);
    }
    public void Grant(AccessScopeKind kind, Guid? id, DateTimeOffset? effectiveTo = null) =>
        Grants.Add(new(LeaveTeamCalendarPermissions.Read, false, Guid.NewGuid(), kind, id)
            { AuthorityTimingKnown = true, ValidFromUtc = Clock.Now.AddDays(-1), ValidUntilUtc = effectiveTo });
    public Task<Cosmix.Application.Common.Models.PagedResult<LeaveTeamCalendarEntry>> Team(Guid[]? ids = null,
        DateOnly? from = null, DateOnly? to = null, int page = 1, int pageSize = 100) =>
        Service.TeamAsync(new(ids ?? [Employee], from ?? new(2026, 10, 1), to ?? new(2026, 10, 31), page, pageSize), Context, default);
    public LeaveRow NewLeave(Guid owner, DateOnly[] dates, ApprovalState state)
    {
        var type = new LeaveType("SICK", "PRIVATE medical category", true, true, true, LeaveKind.Sick);
        var request = new LeaveRequest(owner, type.Id, dates.Min(), dates.Max(), dates.Length * 60, "PRIVATE reason",
            "PRIVATE contact", null, "PRIVATE handover", "PRIVATE file reference", LeavePayTreatment.PendingReview, null,
            Identity.AccountId.ToString(), Clock.Now.AddDays(-30));
        foreach (var date in dates)
        {
            var start = new DateTimeOffset(date.ToDateTime(new TimeOnly(4, 0)), TimeSpan.Zero);
            request.Days.Add(new(request.Id, date, start, start.AddHours(1)));
        }
        var approval = new ApprovalRequest("employee-leave-admin", request.Id, request.RequestedByUserId, "PRIVATE snapshot", 1, request.CreatedAt);
        request.Submit(approval);
        List<ApprovalDecision> decisions = [];
        if (state == ApprovalState.Cancelled) approval.Cancel(request.RequestedByUserId, request.CreatedAt.AddMinutes(1));
        else if (state != ApprovalState.Pending) decisions.Add(approval.Decide(state, "synthetic-admin", "Admin", "PRIVATE decision", request.CreatedAt.AddMinutes(1)));
        return new(request, type, approval, decisions, [], null);
    }
    public LeaveRow WithCancellation(LeaveRow parent, ApprovalState state)
    {
        var request = new LeaveCancellationRequest(parent.Request.Id, parent.Request.RequestedByUserId, "PRIVATE cancellation", Clock.Now.AddDays(-20));
        var approval = new ApprovalRequest("employee-leave-cancellation-admin", request.Id, request.RequestedByUserId, "PRIVATE snapshot", 1, request.CreatedAt);
        request.Submit(approval);
        List<ApprovalDecision> decisions = [];
        if (state != ApprovalState.Pending) decisions.Add(approval.Decide(state, "synthetic-admin", "Admin", "PRIVATE decision", request.CreatedAt.AddMinutes(1)));
        return parent with { Cancellations = [new(request, approval, decisions)] };
    }
}
