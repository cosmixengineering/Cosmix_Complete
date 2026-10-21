using System.Collections;
using System.Data.Common;
using System.Linq.Expressions;
using System.Reflection;
using Cosmix.Application.Attendance.Shifts;
using Cosmix.Application.Authentication;
using Cosmix.Application.Authentication.Access;
using Cosmix.Application.HR.EmployeeSelfService;
using Cosmix.Domain.Authentication;
using Cosmix.Domain.Common.Approvals;
using Cosmix.Domain.Common.Audit;
using Cosmix.Domain.HR.AdvancesLoans;
using Cosmix.Domain.HR.Compensation;
using Cosmix.Infrastructure.Database;
using Cosmix.Infrastructure.Repositories.HR;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.Extensions.DependencyInjection;

// Production service and repository predicates, with synthetic IO and a connection-denying interceptor.
var failures = new List<string>();
var checks = 0;
void Check(bool value, string label) { checks++; if (!value) failures.Add(label); }
async Task Denied(EssFixture f, string code, string label)
{
    try { await f.Read(); Check(false, label); }
    catch (AuthenticationFailure e) { Check(e.Code == code, label + ": " + e.Code); }
}
var generic = new EssFixture();
generic.Grants.Clear(); generic.Grant(AccessPermissionCatalog.SelfRead);
await Denied(generic, "forbidden", "generic auth.self.read cannot disclose salary");
Check(generic.Reads == 0, "denied salary permission does not query private financial data");
var allowed = new EssFixture();
await allowed.Read();
Check(allowed.Reads == 1 && allowed.LastEmployee == allowed.Identity.EmployeeId, "financial identity comes only from current employee");
Check(allowed.Audits.Count == 1 && allowed.Audits[0].EventType == "ess.finances.read" &&
    allowed.Audits[0].ActorRoleCode == "SyntheticSelfRole", "financial read retains actual-role audit");
Check(allowed.Transactions == 1 && allowed.Saves == 1, "read authorization and audit share existing transaction");
var revoked = new EssFixture { SessionCurrent = false };
await Denied(revoked, "invalid_authentication", "revoked session cannot read salary");
Check(revoked.Reads == 0, "revoked session does not query financial data");
var unlink = new EssFixture(); unlink.Identity = unlink.Identity with { EmployeeId = Guid.Empty };
await Denied(unlink, "unlinked_employee_account", "unlinked account cannot read financial overview");
var midSession = new EssFixture(); midSession.DuringRead = () => midSession.SessionCurrent = false;
await Denied(midSession, "invalid_authentication", "revocation during IO prevents disclosure");
var midGrant = new EssFixture(); midGrant.DuringRead = () => midGrant.Grants.Clear();
await Denied(midGrant, "forbidden", "permission removed during IO prevents disclosure");
var wrongScope = new EssFixture(); wrongScope.Grants.Clear();
wrongScope.Grant(EmployeeSelfServicePermissions.FinancesRead, AccessScopeKind.Employee, Guid.NewGuid());
await Denied(wrongScope, "forbidden", "another employee scope cannot grant own financial read");
var auditFail = new EssFixture { FailSave = true };
try { await auditFail.Read(); Check(false, "failed audit persistence cannot return private financial data"); }
catch (InvalidOperationException e) { Check(e.Message == "synthetic audit failure", "audit failure propagated"); }

var guard = new NoConnections();
using var db = new CosmixDbContext(new DbContextOptionsBuilder<CosmixDbContext>()
    .UseSqlServer("Server=ess-finances.invalid;Database=NeverConnect;Integrated Security=true;Encrypt=true")
    .ReplaceService<IAsyncQueryProvider, MemoryQueries>().AddInterceptors(guard).Options);
using var services = new ServiceCollection().AddSingleton(db).AddSingleton<TimeProvider>(new EssClock()).BuildServiceProvider();
var repository = ActivatorUtilities.CreateInstance<EmployeeSelfServiceRepository>(services);
var employee = Guid.NewGuid();
var now = new EssClock().GetUtcNow();
var today = new DateOnly(2026, 10, 15);
CompensationRecord Salary(decimal amount, DateOnly from, DateOnly? to = null) => new(employee, 1, null, amount, amount,
    "PKR", Enum.GetValues<WageBasis>()[0], Enum.GetValues<PaymentMethod>()[0], from, to, null, "synthetic", now);
Rows.Add(Salary(1000, new(2020, 1, 1)));
Rows.Add(Salary(2000, new(2099, 1, 1)));
var salary = await repository.GetFinancialOverviewAsync(employee, default);
Check(salary.CurrentCompensation?.BaseAmount == 1000, "future-dated salary is not current salary");
Rows.Clear(); Rows.Add(Salary(3000, new(2020, 1, 1), new(2021, 1, 1)));
Check((await repository.GetFinancialOverviewAsync(employee, default)).CurrentCompensation is null,
    "expired compensation is unavailable, not carried into current view");
Rows.Clear(); Rows.Add(Salary(4000, today, today.AddDays(1)));
Check((await repository.GetFinancialOverviewAsync(employee, default)).CurrentCompensation?.BaseAmount == 4000,
    "local business-day effective start is inclusive");
Rows.Clear(); Rows.Add(Salary(4000, today.AddDays(-1), today));
Check((await repository.GetFinancialOverviewAsync(employee, default)).CurrentCompensation is null,
    "local business-day effective end is exclusive");
Rows.Clear();
var request = new AdvanceLoanRequest(employee, AdvanceLoanKind.Loan, 1000, "PKR", "Synthetic", today, "synthetic-owner", now);
var approval = new ApprovalRequest("synthetic-loan", request.Id, "synthetic-owner", "synthetic-private", 1, now);
approval.Decide(ApprovalState.Approved, "synthetic-reviewer", "SyntheticFinance", "Synthetic approval", now, "synthetic-evidence");
var agreement = new AdvanceLoanAgreement(request.Id, 1000, "PKR", today, "synthetic-terms", approval.Id, "synthetic", now);
typeof(AdvanceLoanAgreement).GetProperty(nameof(AdvanceLoanAgreement.AdvanceLoanRequest))!.SetValue(agreement, request);
typeof(AdvanceLoanAgreement).GetProperty(nameof(AdvanceLoanAgreement.ApprovalRequest))!.SetValue(agreement, approval);
Rows.Add(agreement);
RepaymentLedgerEntry Payment(decimal amount, string key, RepaymentEntryKind kind = RepaymentEntryKind.ReceivedPayment, Guid? reverses = null)
{
    var row = new RepaymentLedgerEntry(agreement.Id, null, kind, amount, "PKR", today, "synthetic-receipt", key, reverses, "synthetic", now);
    typeof(RepaymentLedgerEntry).GetProperty(nameof(RepaymentLedgerEntry.AdvanceLoanAgreement))!.SetValue(row, agreement);
    Rows.Add(row); return row;
}
var paid = Payment(100, "synthetic-100");
Payment(-100, "synthetic-reversed-100", RepaymentEntryKind.Reversal, paid.Id);
var reversed = await repository.GetFinancialOverviewAsync(employee, default);
Check(reversed.ActiveAgreements.Single().RepaidAmount == 0 && reversed.ActiveAgreements.Single().RemainingBalance == 1000,
    "negative reversal restores outstanding principal, without double-negation");
for (var n = 0; n < 25; n++) Payment(10, "synthetic-small-" + n);
var all = await repository.GetFinancialOverviewAsync(employee, default);
Check(all.ActiveAgreements.Single().RepaidAmount == 250 && all.ActiveAgreements.Single().RemainingBalance == 750,
    "outstanding uses whole signed ledger, not recent twenty entries");
Check(all.RecentRepayments.Count == 20, "history window remains bounded independently of balance calculation");
Check((await repository.GetFinancialOverviewAsync(Guid.NewGuid(), default)).ActiveAgreements.Count == 0,
    "repository financial projection remains employee-filtered");
Check(guard.Attempts == 0 && db.ChangeTracker.Entries().Count() == 0, "no SQL connection or tracked write");
foreach (var failure in failures) Console.Error.WriteLine("FAIL: " + failure);
Console.WriteLine($"ESS financial boundary: {checks - failures.Count}/{checks}; synthetic IO/predicates, not HTTP/SQL acceptance.");
return failures.Count == 0 ? 0 : 1;

public sealed class EssClock : TimeProvider
{ public override DateTimeOffset GetUtcNow() => new(2026, 10, 14, 20, 0, 0, TimeSpan.Zero); }
public class Proxy : DispatchProxy
{
    public Func<MethodInfo, object?[], object?> Handler = null!;
    protected override object? Invoke(MethodInfo? m, object?[]? a) => Handler(m!, a ?? []);
    public static T Make<T>(Func<MethodInfo, object?[], object?> handler) where T : class
    { var value = Create<T, Proxy>(); ((Proxy)(object)value).Handler = handler; return value; }
}
public sealed class EssFixture
{
    public AuthenticatedAccount Identity = new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 1, SessionClient.Mobile, false);
    public List<PermissionGrant> Grants = [];
    public List<AuditEvent> Audits = [];
    public bool SessionCurrent = true, FailSave;
    public int Reads, Transactions, Saves;
    public Guid LastEmployee;
    public Action? DuringRead;
    private readonly EmployeeSelfService service;
    public EssFixture()
    {
        Grant(EmployeeSelfServicePermissions.FinancesRead);
        var repository = Proxy.Make<IEmployeeSelfServiceRepository>((m, a) =>
        {
            if (m.Name != "GetFinancialOverviewAsync") throw new Exception(m.Name);
            Reads++; LastEmployee = (Guid)a[0]!; DuringRead?.Invoke();
            return Task.FromResult(new EssFinancialOverviewResponse(null, [], [], [], [], null, []));
        });
        var access = Proxy.Make<IAccountAccessReader>((_, _) => Task.FromResult<IReadOnlyList<PermissionGrant>>(Grants.ToArray()));
        var auth = Proxy.Make<IAccessManagementRepository>((m, a) =>
        {
            if (m.Name == "InTransactionAsync") { Transactions++; return ((Delegate)a[0]!).DynamicInvoke(a[1]); }
            if (m.Name == "GetAssignmentRoleCodeAsync") return Task.FromResult<string?>("SyntheticSelfRole");
            throw new Exception(m.Name);
        });
        var journal = Proxy.Make<ISchedulingJournalRepository>((m, a) =>
        {
            if (m.Name == "IsCurrentSessionAsync") return Task.FromResult(SessionCurrent);
            if (m.Name == "AddAudit") { Audits.Add((AuditEvent)a[0]!); return null; }
            if (m.Name == "SaveChangesAsync")
            { Saves++; return FailSave ? Task.FromException(new InvalidOperationException("synthetic audit failure")) : Task.CompletedTask; }
            throw new Exception(m.Name);
        });
        using var provider = new ServiceCollection().AddSingleton(repository).AddSingleton(access).AddSingleton(auth).AddSingleton(journal)
            .AddSingleton(Proxy.Make<ICurrentAuthentication>((_, _) => Identity)).AddSingleton<TimeProvider>(new EssClock()).BuildServiceProvider();
        service = ActivatorUtilities.CreateInstance<EmployeeSelfService>(provider);
    }
    public void Grant(string permission, AccessScopeKind scope = AccessScopeKind.Self, Guid? id = null) =>
        Grants.Add(new(permission, false, Guid.NewGuid(), scope, scope == AccessScopeKind.Self ? Identity.EmployeeId : id));
    public Task<EssFinancialOverviewResponse> Read()
    {
        var method = typeof(EmployeeSelfService).GetMethod("GetFinancialOverviewAsync")!;
        // Supports the prior signature solely to demonstrate RED against the frozen baseline.
        return (Task<EssFinancialOverviewResponse>)method.Invoke(service, method.GetParameters().Length == 1
            ? [CancellationToken.None] : [new AuthRequestContext("127.0.0.1", "synthetic-ess-financial"), CancellationToken.None])!;
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
