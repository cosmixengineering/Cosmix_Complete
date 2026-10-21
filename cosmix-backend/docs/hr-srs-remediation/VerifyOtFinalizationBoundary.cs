using System.Collections;
using System.Data.Common;
using System.Linq.Expressions;
using System.Reflection;
using System.Text.Json;
using Cosmix.Application.Attendance.MonthLocks;
using Cosmix.Application.Attendance.Overtime;
using Cosmix.Application.Common.Files;
using Cosmix.Domain.Attendance.Overtime;
using Cosmix.Domain.Attendance.WorkSessions;
using Cosmix.Domain.Common.Approvals;
using Cosmix.Infrastructure.Database;
using Cosmix.Infrastructure.Repositories.Attendance;
using Cosmix.Infrastructure.Repositories.HR;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Query;

// Executes production repository predicates over synthetic headers, not SQL or real award evidence.
// An unchanged decision binding or a different Finance population must prevent removing the OT guard.
var failures = new List<string>();
var checks = 0;
void Check(bool condition, string name)
{
    checks++;
    if (!condition) failures.Add(name);
}
var connectionGuard = new NoConnections();
using var db = new CosmixDbContext(new DbContextOptionsBuilder<CosmixDbContext>()
    .UseSqlServer("Server=ot-finalization.invalid;Database=NeverConnect;Integrated Security=true;Encrypt=true")
    .ReplaceService<IAsyncQueryProvider, MemoryQueries>().AddInterceptors(connectionGuard).Options);
var months = new MonthLockRepository(db);
var finance = new PayrollHandoffRepository(db);
var september = new DateOnly(2026, 9, 1);
var october = new DateOnly(2026, 10, 1);
var november = new DateOnly(2026, 11, 1);
DateTimeOffset Boundary(DateOnly date) => new DateTimeOffset(date.ToDateTime(TimeOnly.MinValue), TimeSpan.FromHours(5)).ToUniversalTime();
Task<MonthLockOvertimeEvidence[]> LockSources(DateOnly start) =>
    ((IQueryable<MonthLockOvertimeEvidence>)typeof(MonthLockRepository)
        .GetMethod("OvertimeEvidenceQuery", BindingFlags.Instance | BindingFlags.NonPublic)!
        .Invoke(months, [start, start.AddMonths(1)])!).ToArrayAsync();

var sameMonth = Fixture.Create(new DateOnly(2026, 9, 29), new DateTimeOffset(2026, 9, 30, 6, 0, 0, TimeSpan.Zero));
var sameLock = await LockSources(september);
var sameFinance = await finance.OvertimeAsync([sameMonth.Request.EmployeeId], Boundary(september), Boundary(october), default);
Check(sameLock.Length == 1 && sameLock[0].Id == sameMonth.Request.Id,
    "control: Attendance selects the September duty request");
Check(sameFinance.Count == 1 && sameFinance[0].RequestId == sameMonth.Request.Id &&
    sameFinance[0].DecisionId == sameMonth.Decision!.Id &&
    sameFinance[0].ProtectedDecisionReference == sameMonth.Decision.ProtectedChangeReference,
    "control: Finance selects the same-month request and exact decision header");

Fixture.Clear();
var delayed = Fixture.Create(new DateOnly(2026, 9, 30), new DateTimeOffset(2026, 10, 2, 6, 0, 0, TimeSpan.Zero));
var septemberLock = await LockSources(september);
var septemberFinance = await finance.OvertimeAsync([delayed.Request.EmployeeId], Boundary(september), Boundary(october), default);
Check(septemberLock.Length == 1 && septemberLock[0].Id == delayed.Request.Id &&
    septemberLock[0].State == ApprovalState.Approved,
    "Attendance includes September duty approved on October 2");
Check(septemberFinance.Count == 1 && septemberFinance[0].RequestId == delayed.Request.Id,
    "RED: September handoff must consume the September lock's OT member despite its October approval");
var octoberLock = await LockSources(october);
var octoberFinance = await finance.OvertimeAsync([delayed.Request.EmployeeId], Boundary(october), Boundary(november), default);
Check(octoberLock.Length == 0, "October lock has no OT member for September duty");
Check(octoberFinance.Count == 0,
    "RED: October handoff must not rediscover an OT request absent from October lock membership");
Console.WriteLine($"Membership: September lock={septemberLock.Length}, handoff={septemberFinance.Count}; October lock={octoberLock.Length}, handoff={octoberFinance.Count}.");

var beforeDecisionChange = await months.SourcesAsync(september, october, default);
Check(beforeDecisionChange.Issues.Any(x => x.Code is "overtime_payroll_settlement_unavailable" or "overtime_locked_handoff_unavailable" &&
    x.EmployeeId == delayed.Request.EmployeeId && x.BusinessDate == new DateOnly(2026, 9, 30)),
    "current approved-OT blocker remains present before an exact-award consumer exists");
var originalDecisionReference = delayed.Decision!.ProtectedChangeReference;
typeof(ApprovalDecision).GetProperty(nameof(ApprovalDecision.ProtectedChangeReference))!
    .SetValue(delayed.Decision, Fixture.Reference("decision-changed", 'C'));
var changedDecision = await months.SourcesAsync(september, october, default);
Check(changedDecision.Fingerprint != beforeDecisionChange.Fingerprint,
    "RED: replacing the protected OT decision/financial-evidence reference must invalidate month readiness");
typeof(ApprovalDecision).GetProperty(nameof(ApprovalDecision.ProtectedChangeReference))!
    .SetValue(delayed.Decision, originalDecisionReference);
Fixture.Remove<ApprovalDecision>();
var missingDecision = await months.SourcesAsync(september, october, default);
Check(missingDecision.Issues.Any(x => x.EmployeeId == delayed.Request.EmployeeId),
    "missing approved-decision evidence cannot remove the readiness blocker");
Check(missingDecision.Fingerprint != beforeDecisionChange.Fingerprint,
    "removing the exact decision must invalidate month readiness");
Check(missingDecision.Issues.Any(x => x.Code == "overtime_decision_evidence_unavailable"),
    "missing approved decision has its own explicit evidence dependency");
Check((await finance.OvertimeAsync([delayed.Request.EmployeeId], Boundary(october), Boundary(november), default)).Count == 0,
    "Finance does not invent an award when its decision row is missing");

Fixture.Clear();
var wrongSession = Fixture.Create(new DateOnly(2026, 9, 30), new DateTimeOffset(2026, 10, 2, 6, 0, 0, TimeSpan.Zero));
typeof(WorkSession).GetProperty(nameof(WorkSession.EmployeeId))!.SetValue(wrongSession.Session, Guid.NewGuid());
Check((await months.SourcesAsync(september, october, default)).Issues.Any(x => x.Code == "overtime_session_evidence_unavailable"),
    "a mismatched session owner cannot disappear from monthly OT dependencies");
Fixture.Clear();
Fixture.Create(new DateOnly(2026, 9, 30), new DateTimeOffset(2026, 10, 2, 6, 0, 0, TimeSpan.Zero));
Fixture.Remove<ApprovalRequest>();
Check((await months.SourcesAsync(september, october, default)).Issues.Any(x => x.Code == "overtime_approval_evidence_unavailable"),
    "a dangling approval cannot disappear from monthly OT dependencies");
Fixture.Clear();
Fixture.Create(new DateOnly(2026, 9, 30), new DateTimeOffset(2026, 10, 2, 6, 0, 0, TimeSpan.Zero));
Fixture.Remove<WorkSession>();
Check((await months.SourcesAsync(september, october, default)).Issues.Any(x => x.Code == "overtime_session_evidence_unavailable" && x.BusinessDate is null),
    "an undatable dangling session remains an explicit dependency without inventing a duty date");

Fixture.Clear();
var pending = Fixture.Create(new DateOnly(2026, 9, 30), null);
var pendingSources = await months.SourcesAsync(september, october, default);
Check(pendingSources.Issues.Any(x => x.Code == "pending_overtime" && x.EmployeeId == pending.Request.EmployeeId),
    "pending OT remains a month-finalization blocker");
Check((await finance.OvertimeAsync([pending.Request.EmployeeId], Boundary(september), Boundary(october), default)).Count == 0,
    "pending OT is not a Finance award");
Check(db.ChangeTracker.Entries().Count() == 0, "synthetic rows are never attached for persistence");
Check(connectionGuard.Attempts == 0, "no database connection attempted");
foreach (var failure in failures) Console.Error.WriteLine("FAIL: " + failure);
Console.WriteLine($"OT finalization boundary: {checks - failures.Count}/{checks} checks passed, {failures.Count} failures. Synthetic repository execution; no SQL/host/provider calls.");
return failures.Count == 0 ? 0 : 1;

static class Fixture
{
    private static readonly Dictionary<Type, IList> Data = [];
    public static (OvertimeRequest Request, ApprovalDecision? Decision, WorkSession Session) Create(DateOnly day, DateTimeOffset? approvedAt)
    {
        var employee = Guid.NewGuid();
        var requester = Guid.NewGuid().ToString();
        var site = Guid.NewGuid();
        var start = new DateTimeOffset(day.ToDateTime(new TimeOnly(4, 0)), TimeSpan.Zero);
        var session = new WorkSession(Guid.NewGuid(), 1, employee, day, start, start.AddHours(11),
            Guid.NewGuid(), null, start.AddHours(12), null, correctionId: Guid.NewGuid(), siteId: site);
        var requestedAt = start.AddHours(12);
        var sourceReference = Reference("submission", 'A');
        var approval = new ApprovalRequest("attendance.overtime", Guid.NewGuid(), requester, sourceReference, 1, requestedAt);
        var request = new OvertimeRequest(employee, session.Id, start.AddHours(9), start.AddHours(11), 120,
            Guid.NewGuid(), OvertimeFormula.Version, "Synthetic OT selection fixture", sourceReference, requester,
            requestedAt, approval.Id, siteId: site, jobReference: "SYNTHETIC-OT");
        ApprovalDecision? decision = approvedAt.HasValue
            ? approval.Decide(ApprovalState.Approved, Guid.NewGuid().ToString(), "SyntheticReviewer",
                "Synthetic approved header", approvedAt.Value, Reference("decision", 'B')) : null;
        Add(session); Add(approval); Add(request);
        if (decision is not null) Add(decision);
        return (request, decision, session);
    }
    public static string Reference(string key, char digest) =>
        JsonSerializer.Serialize(new PrivateFileReference("synthetic-" + key, "1", new string(digest, 64), "synthetic", 128));
    private static void Add<T>(T row) where T : class
    {
        if (!Data.TryGetValue(typeof(T), out var rows)) Data[typeof(T)] = rows = new List<T>();
        rows.Add(row);
    }
    public static IQueryable Rows(Type type)
    {
        if (!Data.TryGetValue(type, out var rows)) rows = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(type))!;
        return (IQueryable)typeof(Queryable).GetMethods().Single(x => x.Name == "AsQueryable" && x.IsGenericMethod)
            .MakeGenericMethod(type).Invoke(null, [rows])!;
    }
    public static void Clear() => Data.Clear();
    public static void Remove<T>() => Data.Remove(typeof(T));
}

// Same EF query-root substitution used by VerifyHrReportQueries; repository predicates remain production code.
public sealed class MemoryQueries : IAsyncQueryProvider
{
    public IQueryable CreateQuery(Expression expression) => (IQueryable)Activator.CreateInstance(
        typeof(MemoryQuery<>).MakeGenericType(expression.Type.GetGenericArguments()[0]), this, expression)!;
    public IQueryable<T> CreateQuery<T>(Expression expression) => new MemoryQuery<T>(this, expression);
    public object? Execute(Expression expression) => Expression.Lambda(new ReplaceRoots().Visit(expression)!).Compile().DynamicInvoke();
    public T Execute<T>(Expression expression) => (T)Execute(expression)!;
    public T ExecuteAsync<T>(Expression expression, CancellationToken ct = default) =>
        (T)Activator.CreateInstance(typeof(MemoryQuery<>).MakeGenericType(typeof(T).GetGenericArguments()[0]), this, expression)!;
    private sealed class ReplaceRoots : ExpressionVisitor
    {
        protected override Expression VisitExtension(Expression node) => node is EntityQueryRootExpression root
            ? Fixture.Rows(root.EntityType.ClrType).Expression : base.VisitExtension(node);
        protected override Expression VisitMethodCall(MethodCallExpression node) =>
            node.Method.DeclaringType == typeof(EntityFrameworkQueryableExtensions) && node.Method.Name == "AsNoTracking"
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
    {
        await Task.CompletedTask;
        foreach (var item in this) { ct.ThrowIfCancellationRequested(); yield return item; }
    }
}
sealed class NoConnections : DbConnectionInterceptor
{
    public int Attempts;
    public override InterceptionResult ConnectionOpening(DbConnection connection, ConnectionEventData eventData, InterceptionResult result)
    { Attempts++; throw new Exception("SQL connections forbidden"); }
    public override ValueTask<InterceptionResult> ConnectionOpeningAsync(DbConnection connection, ConnectionEventData eventData,
        InterceptionResult result, CancellationToken ct = default)
    { Attempts++; throw new Exception("SQL connections forbidden"); }
}
