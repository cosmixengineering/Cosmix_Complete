using System.Collections;
using System.Data.Common;
using System.Linq.Expressions;
using System.Reflection;
using System.Text.Json;
using Cosmix.Application.Attendance.Common;
using Cosmix.Application.Attendance.DailySummaries;
using Cosmix.Application.Attendance.Rosters;
using Cosmix.Application.Attendance.Shifts;
using Cosmix.Application.Authentication;
using Cosmix.Application.Authentication.Access;
using Cosmix.Application.Common.Files;
using Cosmix.Application.HR.EmployeeSelfService;
using Cosmix.Application.HR.Leaves;
using Cosmix.Domain.Attendance.DailySummaries;
using Cosmix.Domain.Attendance.WorkSessions;
using Cosmix.Domain.Authentication;
using Cosmix.Domain.Common.Approvals;
using Cosmix.Domain.Common.Audit;
using Cosmix.Domain.HR.EmployeeAssignments;
using Cosmix.Domain.HR.Employees;
using Cosmix.Domain.HR.Leaves;
using Cosmix.Domain.HR.Policies;
using Cosmix.Infrastructure.Database;
using Cosmix.Infrastructure.Repositories.HR;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.Extensions.DependencyInjection;

var failures = new List<string>();
var checks = 0;
void Check(bool value, string label) { checks++; if (!value) failures.Add(label); }
var guard = new NoConnections();
using var db = new CosmixDbContext(new DbContextOptionsBuilder<CosmixDbContext>()
    .UseSqlServer("Server=ess-consistency.invalid;Database=NeverConnect;Integrated Security=true;Encrypt=true")
    .ReplaceService<IAsyncQueryProvider, MemoryQueries>().AddInterceptors(guard).Options);
var clock = new EssClock();
var repository = new EmployeeSelfServiceRepository(db, clock);
var employee = new Employee(clock.GetUtcNow(), "synthetic");
var today = new DateOnly(2026, 10, 15);
EmployeeAssignment Assignment(DateOnly from, DateOnly? to = null) => new(employee.Id, Guid.NewGuid(), Guid.NewGuid(),
    Guid.NewGuid(), Guid.NewGuid(), null, null, null, from, to, "synthetic", clock.GetUtcNow());
Rows.Add(employee);
var current = Assignment(today.AddDays(-10));
Rows.Add(current); Rows.Add(Assignment(new(2099, 1, 1)));
Check((await repository.GetProfileAsync(employee.Id, default))?.ActiveAssignment?.AssignmentId == current.Id,
    "future assignment cannot replace current employment");
Rows.Clear(); Rows.Add(employee); Rows.Add(Assignment(today.AddDays(-10), today));
Check((await repository.GetProfileAsync(employee.Id, default))?.ActiveAssignment is null,
    "employment effective end is exclusive on Karachi business date");
Rows.Clear(); Rows.Add(employee); Rows.Add(Assignment(today));
Check((await repository.GetProfileAsync(employee.Id, default))?.ActiveAssignment?.EffectiveFrom == today,
    "employment effective start is inclusive across UTC/local midnight");
Rows.Add(Assignment(today.AddDays(-1)));
try
{
    var profile = await repository.GetProfileAsync(employee.Id, default);
    Check(profile?.ActiveAssignment is null && JsonSerializer.Serialize(profile).Contains("conflict", StringComparison.Ordinal),
        "overlapping employment is explicit conflict, never arbitrary latest row");
}
catch (AuthenticationFailure e) { Check(e.StatusCode == 409, "overlapping employment rejects conflicting evidence"); }
Rows.Clear(); Rows.Add(new LeaveType("CL", "Synthetic casual", false, true, true, LeaveKind.Casual));
// Exercise the legacy methods only while proving RED against the retained pre-fix assembly.
var legacyLeave = typeof(EmployeeSelfServiceRepository).GetMethod("GetLeaveOverviewAsync");
if (legacyLeave is not null)
{
    var task = (Task)legacyLeave.Invoke(repository, [employee.Id, CancellationToken.None])!;
    await task;
    var json = JsonSerializer.Serialize(task.GetType().GetProperty("Result")!.GetValue(task));
    Check(!json.Contains("\"EntitledDays\":12", StringComparison.Ordinal), "missing policy cannot invent twelve casual days");
}
var legacyCalendar = typeof(EmployeeSelfServiceRepository).GetMethod("GetAttendanceCalendarAsync");
if (legacyCalendar is not null)
{
    var task = (Task)legacyCalendar.Invoke(repository, [employee.Id, 2026, 10, CancellationToken.None])!;
    await task;
    var json = JsonSerializer.Serialize(task.GetType().GetProperty("Result")!.GetValue(task));
    Check(!json.Contains("\"Status\":\"RestDay\"", StringComparison.Ordinal), "Sunday without roster evidence is not a rest day");
}
Rows.Clear();
var f = new ServiceFixture(repository);
async Task Fails(Func<Task> action, string code, string label, int? status = null)
{
    try { await action(); Check(false, label); }
    catch (AuthenticationFailure e) { Check(e.Code == code && (!status.HasValue || e.StatusCode == status), label + ": " + e.Code); }
}
var overview = await f.Service.GetLeaveOverviewAsync(f.Context, default);
Check(overview.Status == "incomplete" && overview.Balances.Single().Balance is null &&
    overview.Balances.Single().Reasons.Contains("leave_policy_unavailable"), "no policy is explicit unavailable with no quota");
f.Policies.Add(f.NewPolicy());
overview = await f.Service.GetLeaveOverviewAsync(f.Context, default);
var balance = overview.Balances.Single().Balance!;
Check(balance.TypeBalance?.LimitDays == 10 && balance.TypeBalance.UsedDays == new LeaveExactDays("2", "3") &&
    balance.TypeBalance.RemainingDays == new LeaveExactDays("28", "3"), "ESS retains exact thirds and configured quota instead of integer days");
Check(balance.FiscalYearStart == new DateOnly(2026, 1, 1) && balance.FiscalYearEndExclusive == new DateOnly(2027, 1, 1),
    "configured fiscal year is retained instead of calendar or legacy July assumption");
Check(balance.AsOf == today && overview.AsOf == today, "leave overview uses injected Karachi business date");
f.Usage = new(120, 120, [new(240, 480, LeaveKind.Casual), new(-120, 480, LeaveKind.Casual)]);
balance = await f.Service.GetLeaveBalanceAsync(new(f.Type.Id, f.Policies[0].Id), f.Context, default);
Check(balance.TypeBalance?.UsedDays == new LeaveExactDays("1", "4") &&
    balance.TypeBalance.RemainingDays == new LeaveExactDays("39", "4"), "signed release preserves original policy denominator");
f.Usage = new(1, 1);
balance = await f.Service.GetLeaveBalanceAsync(new(f.Type.Id, f.Policies[0].Id), f.Context, default);
Check(balance.Status == "conflict" && balance.TypeBalance is null && balance.AnnualBalance is null,
    "legacy minutes without immutable units cannot masquerade as zero balance");
f.Usage = new(0, 0, []); f.Policies.Add(f.NewPolicy());
overview = await f.Service.GetLeaveOverviewAsync(f.Context, default);
Check(overview.Balances.Single().Status == "selection_required" && overview.Balances.Single().Balance is null &&
    overview.Balances.Single().ApplicablePolicies.Count == 2, "multiple policies do not pick an arbitrary winner");
Check((await f.Service.GetLeaveBalanceAsync(new(f.Type.Id, f.Policies[1].Id), f.Context, default)).Status == "available",
    "explicit selection resolves an eligible published policy");
balance = await f.Service.GetLeaveBalanceAsync(new(f.Type.Id), f.Context, default);
Check(balance.Status == "unavailable" && balance.TypeBalance is null, "explicit balance without selection stays unavailable");
f.ProvenanceValid = false;
Check((await f.Service.GetLeaveBalanceAsync(new(f.Type.Id, f.Policies[0].Id), f.Context, default)).Status == "conflict",
    "assignment/calendar provenance conflict flows through authoritative balance");
f.ProvenanceValid = true; f.TransitionConflict = true;
Check((await f.Service.GetLeaveOverviewAsync(f.Context, default)).Balances.Single().Reasons.Contains("leave_policy_year_transition_unavailable"),
    "fiscal transition conflicts remain visible");
f.TransitionConflict = false;

var calendar = await f.Service.GetAttendanceCalendarAsync(2026, 10, f.Context, default);
Check(calendar.Days.Count == 31 && calendar.RestDays is null && calendar.TotalOvertimeMinutes is null && calendar.Status == "incomplete",
    "missing policy does not synthesize Sunday rest or zero overtime");
Check(calendar.Days[3].Policy.DayStatus is null && calendar.Days[3].Roster.Status == "unassigned",
    "Sunday remains explicit missing roster/classification evidence");
f.Scheduled = true; f.Calculated = true;
var key = Guid.NewGuid(); var start = new DateTimeOffset(2026, 10, 1, 4, 0, 0, TimeSpan.Zero);
f.Sessions.Add(new(key, 1, f.Identity.EmployeeId, new(2026, 10, 1), start, start.AddMinutes(60), f.FirstShift,
    null, start.AddHours(3), null, correctionId: Guid.NewGuid()));
f.Sessions.Add(new(key, 2, f.Identity.EmployeeId, new(2026, 10, 1), start, start.AddMinutes(90), f.FirstShift,
    null, start.AddHours(3), null, correctionId: Guid.NewGuid()));
calendar = await f.Service.GetAttendanceCalendarAsync(2026, 10, f.Context, default);
Check(calendar.TotalWorkedMinutes == 90, "calendar counts latest session revision once, not both revisions");
Check(calendar.Days[0].Roster.ShiftVersionId == f.FirstShift && calendar.Days[15].Roster.ShiftVersionId == f.SecondShift,
    "per-date roster follows mid-month transfer");
Check(calendar.PresentDays == 31 && calendar.RestDays == 0 && calendar.TotalOvertimeMinutes == 31,
    "complete monthly totals derive only from authoritative per-day policy values");
f.Clock.Now = new(2026, 9, 30, 20, 0, 0, TimeSpan.Zero);
calendar = await f.Service.GetAttendanceCalendarAsync(null, null, f.Context, default);
Check(calendar.Year == 2026 && calendar.Month == 10, "default calendar month follows Karachi at UTC month boundary");
await Fails(() => f.Service.GetAttendanceCalendarAsync(2026, 13, f.Context, default), "invalid_ess_calendar_month", "invalid month is rejected instead of silently replaced");
f.Locked = true;
await Fails(() => f.Service.GetAttendanceCalendarAsync(2026, 10, f.Context, default), "locked_attendance_summary_unavailable",
    "locked month without frozen evidence cannot fall back to live rows");
f.Locked = false; f.SessionCurrent = false;
await Fails(() => f.Service.GetLeaveOverviewAsync(f.Context, default), "invalid_authentication", "revoked session cannot read own leave");
f.SessionCurrent = true; f.AfterRead = () => f.SessionCurrent = false;
await Fails(() => f.Service.GetLeaveBalanceAsync(new(f.Type.Id, f.Policies[0].Id), f.Context, default), "invalid_authentication",
    "revocation during balance IO prevents disclosure");
f.AfterRead = null; f.SessionCurrent = true;
f.Grants.RemoveAll(x => x.Permission == LeavePermissions.Self);
await Fails(() => f.Service.GetLeaveOverviewAsync(f.Context, default), "access_denied", "ESS grant cannot bypass authoritative leave permission");
f.Grant(LeavePermissions.Self); f.AfterRead = () => f.Grants.RemoveAll(x => x.Permission == EmployeeSelfServicePermissions.LeavesRead);
await Fails(() => f.Service.GetLeaveBalanceAsync(new(f.Type.Id, f.Policies[0].Id), f.Context, default), "forbidden",
    "removed ESS grant during IO prevents disclosure");
f.AfterRead = null; f.Grant(EmployeeSelfServicePermissions.LeavesRead); f.FailAudit = true;
await Fails(() => f.Service.GetLeaveBalanceAsync(new(f.Type.Id, f.Policies[0].Id), f.Context, default), "synthetic_audit_failed",
    "failed authoritative read audit prevents disclosure");
Check(f.Audits.Count > 0, "authoritative reads retain audit events");
f.FailAudit = false; f.AfterRead = () => f.Grants.RemoveAll(x => x.Permission == LeavePermissions.Self);
var auditsBeforeSourceRevocation = f.Audits.Count;
await Fails(() => f.Service.GetLeaveBalanceAsync(new(f.Type.Id, f.Policies[0].Id), f.Context, default), "access_denied",
    "authoritative read denies source permission revoked during IO", 403);
Check(f.Audits.Count == auditsBeforeSourceRevocation, "denied source read does not record a successful read audit");
f.AfterRead = null; f.Grant(LeavePermissions.Self);

Rows.Clear();
LeaveRequest Request(Guid owner, DateOnly date, ApprovalState? state, Guid? previous = null)
{
    var row = new LeaveRequest(owner, f.Type.Id, date, date, 120, "Synthetic partial leave", null, null, null, null,
        LeavePayTreatment.PendingReview, null, "synthetic", clock.GetUtcNow(), previousRequestId: previous);
    if (state.HasValue)
    {
        var approval = new ApprovalRequest(LeaveRules.RequestType, row.Id, "synthetic", "private", 1, clock.GetUtcNow());
        row.Submit(approval);
        if (state != ApprovalState.Pending) approval.Decide(state.Value, "reviewer", "Admin", "Synthetic decision", clock.GetUtcNow(), "evidence");
    }
    Rows.Add(row); return row;
}
var pending = Request(employee.Id, today, ApprovalState.Pending);
Request(employee.Id, today, null);
var superseded = Request(employee.Id, today, ApprovalState.Pending);
Request(employee.Id, today, ApprovalState.Returned, superseded.Id);
Request(Guid.NewGuid(), today, ApprovalState.Pending);
Check((await repository.GetRequestsSummaryAsync(employee.Id, default)).PendingLeaveRequestsCount == 1,
    "pending summary excludes unsubmitted, superseded and another employee requests");
for (var i = 0; i < 6; i++)
{
    var cancelled = Request(employee.Id, today, ApprovalState.Approved);
    var cancellation = new LeaveCancellationRequest(cancelled.Id, "synthetic", "Synthetic cancellation", clock.GetUtcNow());
    var approval = new ApprovalRequest(LeaveRules.CancellationType, cancellation.Id, "synthetic", "private", 1, clock.GetUtcNow());
    cancellation.Submit(approval);
    approval.Decide(ApprovalState.Approved, "reviewer", "Admin", "Synthetic decision", clock.GetUtcNow(), "evidence");
    Rows.Add(cancellation);
}
var upcoming = Request(employee.Id, today.AddDays(1), ApprovalState.Approved);
Request(Guid.NewGuid(), today, ApprovalState.Approved);
var selection = await repository.GetLeaveRequestIdsAsync(employee.Id, today, default);
Check(selection.Upcoming.SequenceEqual(new[] { upcoming.Id }), "cancelled leave is excluded before top-five limit and employee scope is retained");
Check(selection.Recent.Contains(pending.Id) && selection.Recent.Contains(upcoming.Id), "recent history retains partial leave independently of upcoming filter");
var partial = Request(f.Identity.EmployeeId, today.AddDays(1), ApprovalState.Pending);
var partialStart = new DateTimeOffset(2026, 10, 16, 4, 0, 0, TimeSpan.Zero);
partial.Days.Add(new(partial.Id, today.AddDays(1), partialStart, partialStart.AddMinutes(120), f.FirstShift));
f.Requests.Add(partial.Id, new(partial, f.Type, partial.ApprovalRequest, [], [], null));
overview = await f.Service.GetLeaveOverviewAsync(f.Context, default);
Check(overview.RecentRequests.Single().RequestedMinutes == 120 && overview.RecentRequests.Single().Days.Single().Minutes == 120,
    "request history retains exact two-hour allocation instead of one calendar day");
Check(overview.UpcomingApprovedLeaves.Count == 0, "pending partial leave is not described as approved");
Check((await f.Service.GetLeaveBalanceAsync(new(f.Type.Id, f.Policies[0].Id), f.Context, default)).TypeBalance?.RemainingDays == new LeaveExactDays("10", "1"),
    "pending request is not silently deducted as a quota reservation");
Check(guard.Attempts == 0 && !db.ChangeTracker.Entries().Any(), "no SQL or tracked writes");
foreach (var failure in failures) Console.Error.WriteLine("FAIL: " + failure);
Console.WriteLine($"ESS consistency: {checks - failures.Count}/{checks}; actual services/predicates with synthetic IO, not HTTP/SQL acceptance.");
return failures.Count == 0 ? 0 : 1;

public sealed class EssClock : TimeProvider
{
    public DateTimeOffset Now = new(2026, 10, 14, 20, 0, 0, TimeSpan.Zero);
    public override DateTimeOffset GetUtcNow() => Now;
}
public class Proxy : DispatchProxy
{
    public Func<MethodInfo, object?[], object?> Handler = null!;
    protected override object? Invoke(MethodInfo? m, object?[]? a) => Handler(m!, a ?? []);
    public static T Make<T>(Func<MethodInfo, object?[], object?> handler) where T : class
    { var value = Create<T, Proxy>(); ((Proxy)(object)value).Handler = handler; return value; }
}
public sealed class ServiceFixture
{
    public EssClock Clock = new();
    public AuthenticatedAccount Identity = new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 1, SessionClient.Mobile, false);
    public Guid Category = Guid.NewGuid(), FirstShift = Guid.NewGuid(), SecondShift = Guid.NewGuid();
    public bool SessionCurrent = true, ProvenanceValid = true, TransitionConflict, Scheduled, Calculated, Locked, FailAudit;
    public List<PermissionGrant> Grants = [];
    public List<AuditEvent> Audits = [];
    public List<PolicyVersion> Policies = [];
    public List<WorkSession> Sessions = [];
    public Dictionary<Guid, LeaveRow> Requests = [];
    public LeaveType Type = new("CL", "Synthetic casual", false, true, true, LeaveKind.Casual);
    public LeaveBalanceUsage Usage = new(2, 2, [new(1, 3, LeaveKind.Casual), new(1, 3, LeaveKind.Casual)]);
    public Action? AfterRead;
    public EmployeeSelfService Service;
    public AuthRequestContext Context = new("127.0.0.1", "synthetic-ess-consistency");
    public ServiceFixture(IEmployeeSelfServiceRepository repository)
    {
        foreach (var permission in new[] { EmployeeSelfServicePermissions.LeavesRead, EmployeeSelfServicePermissions.AttendanceRead,
            LeavePermissions.Self, AttendanceSummaryPermissions.Self }) Grant(permission);
        var current = Proxy.Make<ICurrentAuthentication>((_, _) => Identity);
        var access = Proxy.Make<IAccountAccessReader>((_, _) => Task.FromResult<IReadOnlyList<PermissionGrant>>(Grants.ToArray()));
        var auth = Proxy.Make<IAccessManagementRepository>((m, a) => m.Name switch
        {
            "InTransactionAsync" => ((Delegate)a[0]!).DynamicInvoke(a[1]),
            "GetAssignmentRoleCodeAsync" => Task.FromResult<string?>("SyntheticSelfRole"),
            _ => throw new Exception(m.Name)
        });
        var journal = Proxy.Make<ISchedulingJournalRepository>((m, a) =>
        {
            if (m.Name == "IsCurrentSessionAsync") return Task.FromResult(SessionCurrent);
            if (m.Name == "AddAudit") { Audits.Add((AuditEvent)a[0]!); return null; }
            if (m.Name == "SaveChangesAsync") return FailAudit
                ? Task.FromException(new AuthenticationFailure("synthetic_audit_failed", 409)) : Task.CompletedTask;
            throw new Exception("Unexpected mutation: " + m.Name);
        });
        var files = Proxy.Make<IPrivateFileStore>((m, _) => throw new Exception("Unexpected private IO: " + m.Name));
        var operation = new AttendanceOperation(current, access, auth, journal,
            Proxy.Make<IAuthenticationRepository>((m, _) => throw new Exception(m.Name)), Clock, files);
        var leaveRepo = Proxy.Make<ILeaveRepository>((m, a) => m.Name switch
        {
            "TypesAsync" => Task.FromResult<IReadOnlyList<LeaveType>>([Type]),
            "EmployeeExistsAsync" => Task.FromResult((Guid)a[0]! == Identity.EmployeeId),
            "TypeAsync" => Task.FromResult<LeaveType?>((Guid)a[0]! == Type.Id ? Type : null),
            "PolicyVersionsAsync" => Task.FromResult<IReadOnlyList<PolicyVersion>>(Policies.ToArray()),
            "PolicyAsync" => PolicyEvidence(a),
            "FindAsync" => Task.FromResult<LeaveRow?>(Requests.TryGetValue((Guid)a[0]!, out var row) &&
                row.Request.EmployeeId == (Guid?)a[1] ? row : null),
            _ => throw new Exception("Unexpected leave IO: " + m.Name)
        });
        var rosters = Proxy.Make<IIndividualRosterResolver>((_, a) =>
        {
            if ((Guid)a[0]! != Identity.EmployeeId) throw new Exception("Wrong employee roster");
            var day = (DateOnly)a[1]!;
            return Task.FromResult(new EffectiveRosterResponse(Identity.EmployeeId, day, Scheduled ? "scheduled" : "unassigned",
                "Asia/Karachi", ShiftVersionId: Scheduled ? day.Day < 16 ? FirstShift : SecondShift : null,
                ConfiguredScheduledMinutes: Scheduled ? 480 : null));
        });
        var leaveService = new LeaveService(operation, leaveRepo, new(operation, files,
            Proxy.Make<IEmployeeFileScanner>((m, _) => throw new Exception(m.Name))), new(operation, files), new(access, auth), rosters, files);
        var summaryRepo = Proxy.Make<IAttendanceSummaryRepository>((m, a) => m.Name switch
        {
            "IsLockedAsync" => Task.FromResult(Locked),
            "LatestAsync" => Task.FromResult<DailyAttendanceSummary?>(null),
            "SessionsAsync" => Task.FromResult<IReadOnlyList<WorkSession>>(Sessions.Where(x => x.EmployeeId == (Guid)a[0]! && x.AttendanceDate == (DateOnly)a[1]!).ToArray()),
            "ExceptionCountAsync" => Task.FromResult(0),
            _ => throw new Exception("Unexpected attendance IO: " + m.Name)
        });
        var calculator = Proxy.Make<IAttendancePolicyCalculator>((_, _) => Task.FromResult(Calculated
            ? new AttendancePolicyResult("calculated", Guid.NewGuid(), "synthetic", 480, 0, 1, AttendanceDayStatus.Present, [])
            : new AttendancePolicyResult("policy_unavailable", null, null, null, null, null, null, ["synthetic_missing_policy"])));
        var attendanceService = new AttendanceSummaryService(operation, summaryRepo, rosters, calculator, files);
        using var provider = new ServiceCollection().AddSingleton(repository).AddSingleton(current).AddSingleton(access)
            .AddSingleton<TimeProvider>(Clock).AddSingleton(auth).AddSingleton(journal).AddSingleton(leaveService)
            .AddSingleton(attendanceService).BuildServiceProvider();
        Service = ActivatorUtilities.CreateInstance<EmployeeSelfService>(provider);
    }
    public void Grant(string permission) => Grants.Add(new(permission, false, Guid.NewGuid(), AccessScopeKind.Self, Identity.EmployeeId));
    public PolicyVersion NewPolicy()
    {
        var rules = new LeavePolicySettings(20, 8, 10, 0, 480, [Category], Guid.NewGuid(), LeaveHolidayTreatment.Exclude, [LeaveKind.Casual])
        { SettingsVersion = 2, YearStartMonth = 1, YearStartDay = 1 };
        var policy = new Policy("SYNTHETIC", "Synthetic leave settings", PolicyCategory.Leave);
        var version = new PolicyVersion(policy.Id, 1, new(2020, 1, 1), null, rules.PublishedSchemaVersion,
            JsonSerializer.Serialize(rules), null, null, "synthetic", Clock.GetUtcNow(), PolicyAuthority.DirectHr);
        typeof(PolicyVersion).GetProperty(nameof(PolicyVersion.Policy))!.SetValue(version, policy);
        return version;
    }
    private Task<LeavePolicyEvidence?> PolicyEvidence(object?[] args)
    {
        if ((Guid)args[0]! != Identity.EmployeeId || (Guid)args[1]! != Type.Id) throw new Exception("Wrong balance identity");
        if (TransitionConflict) throw AttendanceOperation.Conflict("leave_policy_year_transition_unavailable");
        var version = Policies.SingleOrDefault(x => x.Id == (Guid)args[2]!);
        AfterRead?.Invoke();
        return Task.FromResult<LeavePolicyEvidence?>(version is null ? null : new(version, Category, new(2020, 1, 1), ProvenanceValid, [], Usage));
    }
}
static class Rows
{
    private static readonly Dictionary<Type, IList> Data = [];
    public static void Add<T>(T row) where T : class
    { if (!Data.TryGetValue(typeof(T), out var rows)) Data[typeof(T)] = rows = new List<T>(); rows.Add(row); }
    public static void Clear() => Data.Clear();
    public static IQueryable Query(Type type)
    {
        if (!Data.TryGetValue(type, out var rows)) rows = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(type))!;
        return (IQueryable)typeof(Queryable).GetMethods().Single(x => x.Name == "AsQueryable" && x.IsGenericMethod)
            .MakeGenericMethod(type).Invoke(null, [rows])!;
    }
}
public sealed class MemoryQueries : IAsyncQueryProvider
{
    public IQueryable CreateQuery(Expression expression) => (IQueryable)Activator.CreateInstance(
        typeof(MemoryQuery<>).MakeGenericType(expression.Type.GetGenericArguments()[0]), this, expression)!;
    public IQueryable<T> CreateQuery<T>(Expression expression) => new MemoryQuery<T>(this, expression);
    public object? Execute(Expression expression) => Expression.Lambda(new ReplaceRoots().Visit(expression)!).Compile().DynamicInvoke();
    public T Execute<T>(Expression expression) => (T)Execute(expression)!;
    public T ExecuteAsync<T>(Expression expression, CancellationToken ct = default)
    {
        var type = typeof(T).GetGenericArguments()[0];
        if (typeof(T).GetGenericTypeDefinition() == typeof(Task<>))
            return (T)typeof(Task).GetMethod(nameof(Task.FromResult))!.MakeGenericMethod(type).Invoke(null, [Execute(expression)])!;
        return (T)Activator.CreateInstance(typeof(MemoryQuery<>).MakeGenericType(type), this, expression)!;
    }
    private sealed class ReplaceRoots : ExpressionVisitor
    {
        protected override Expression VisitExtension(Expression node) => node is EntityQueryRootExpression root
            ? Rows.Query(root.EntityType.ClrType).Expression : base.VisitExtension(node);
        protected override Expression VisitMethodCall(MethodCallExpression node) =>
            node.Method.DeclaringType == typeof(EntityFrameworkQueryableExtensions) && node.Method.Name is "AsNoTracking" or "Include" or "ThenInclude"
                ? Visit(node.Arguments[0]) : base.VisitMethodCall(node);
    }
}
public sealed class MemoryQuery<T>(MemoryQueries provider, Expression expression) : IOrderedQueryable<T>, IAsyncEnumerable<T>
{
    public Type ElementType => typeof(T);
    public Expression Expression => expression;
    public IQueryProvider Provider => provider;
    public IEnumerator<T> GetEnumerator() => provider.Execute<IEnumerable<T>>(expression).GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    public async IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken ct = default)
    { await Task.CompletedTask; foreach (var item in this) { ct.ThrowIfCancellationRequested(); yield return item; } }
}
sealed class NoConnections : DbConnectionInterceptor
{
    public int Attempts;
    public override InterceptionResult ConnectionOpening(DbConnection c, ConnectionEventData e, InterceptionResult r)
    { Attempts++; throw new Exception("SQL forbidden"); }
    public override ValueTask<InterceptionResult> ConnectionOpeningAsync(DbConnection c, ConnectionEventData e,
        InterceptionResult r, CancellationToken ct = default)
    { Attempts++; throw new Exception("SQL forbidden"); }
}
