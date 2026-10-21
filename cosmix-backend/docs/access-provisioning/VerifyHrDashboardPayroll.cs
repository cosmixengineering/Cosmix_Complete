#:project ../../src/Cosmix.Application/Cosmix.Application.csproj
#:property PublishAot=false
#:property RestorePackagesWithLockFile=false
using System.Reflection;
using System.Text.Json;
using Cosmix.Application.Authentication;
using Cosmix.Application.Authentication.Access;
using Cosmix.Application.Common.Files;
using Cosmix.Application.HR.Compensation;
using Cosmix.Application.HR.Dashboard;
using Cosmix.Application.HR.Employees;
using Cosmix.Application.HR.PayrollHandoff;
using Cosmix.Domain.Authentication;
using Cosmix.Domain.Authentication.Scopes;
using Cosmix.Domain.HR.Compensation;
using Cosmix.Domain.HR.PayrollHandoff;

// Actual dashboard aggregation with synthetic domain sources. No SQL, login, private
// evidence, payroll calculation, posting, settlement or payment is exercised here.
var projection = typeof(HrDashboardPayrollService)
    .GetMethod("Project", BindingFlags.Static | BindingFlags.NonPublic)
    ?? throw new Exception("Source-backed payroll dashboard aggregation is unavailable");
var checks = 0;
void Check(bool condition, string description)
{
    if (!condition) throw new Exception(description);
    checks++;
}
var now = new DateTimeOffset(2026, 10, 19, 8, 0, 0, TimeSpan.Zero);
var date = new DateOnly(2026, 10, 19);
var employee = Guid.NewGuid();
var second = Guid.NewGuid();
var third = Guid.NewGuid();
CompensationRecord Salary(Guid id, decimal amount, WageBasis basis = WageBasis.Monthly,
    string currency = "PKR", DateOnly? start = null, DateOnly? end = null) =>
    new(id, 1, null, amount, amount + 100m, currency, basis, PaymentMethod.Bank,
        start ?? new(2026, 1, 1), end, null, "synthetic-approver", now);
JsonElement Project(IReadOnlyList<Guid> employees, IReadOnlyList<CompensationRecord> candidates,
    int future = 0, int pending = 0, IReadOnlyList<PayrollHandoffBatch>? batches = null,
    IReadOnlyList<PayrollHandoffLine>? lines = null) => JsonSerializer.SerializeToElement(
        projection.Invoke(null, [now, date, "Asia/Karachi", employees, candidates,
            future, pending, batches ?? [], lines ?? []]));
int Count(JsonElement summary, string property) => summary.GetProperty(property).GetInt32();
decimal Amount(JsonElement row, string property) => row.GetProperty(property).GetDecimal();

var missing = Project(Enumerable.Range(0, 12).Select(_ => Guid.NewGuid()).ToArray(), [], future: 2);
Check(Count(missing, "ActiveEmployees") == 12 && Count(missing, "MissingSalaryEmployeeCount") == 12,
    "twelve active employees without current salary remain twelve missing salaries");
Check(Count(missing, "FutureSalaryVersions") == 2 && Count(missing, "MonthlySalaryEmployeeCount") == 0,
    "scheduled salary versions are counted without becoming today's compensation");
Check(missing.GetProperty("EffectiveMonthlySalary").GetArrayLength() == 0 &&
    missing.GetProperty("Handoff").GetArrayLength() == 0,
    "absent salary and absent handoff never fabricate monetary rows");

var effective = Project([employee, second, third], [Salary(employee, 500m), Salary(second, 800m, currency: "USD")], pending: 1);
var money = effective.GetProperty("EffectiveMonthlySalary");
Check(money.GetArrayLength() == 2 && money[0].GetProperty("Currency").GetString() == "PKR" &&
    Amount(money[0], "GrossAmount") == 600m && Amount(money[1], "GrossAmount") == 900m,
    "effective monthly gross salary groups currencies without exchange-rate assumptions");
Check(Count(effective, "MonthlySalaryEmployeeCount") == 2 && Count(effective, "MissingSalaryEmployeeCount") == 1 &&
    Count(effective, "PendingCompensationChangeCount") == 1,
    "partial salary coverage and pending changes remain visible");

var nonmonthly = Project([employee, second], [Salary(employee, 700m, WageBasis.Daily), Salary(second, 20m, WageBasis.Hourly)]);
Check(Count(nonmonthly, "NonMonthlySalaryEmployeeCount") == 2 && Count(nonmonthly, "MissingSalaryEmployeeCount") == 0 &&
    nonmonthly.GetProperty("EffectiveMonthlySalary").GetArrayLength() == 0,
    "daily and hourly compensation is configured but never prorated into monthly salary");
var expired = Project([employee], [Salary(employee, 500m, end: date)]);
Check(Count(expired, "MissingSalaryEmployeeCount") == 1 && expired.GetProperty("EffectiveMonthlySalary").GetArrayLength() == 0,
    "the newest started salary expires at its exclusive end and cannot revive an earlier version");
var futureOnly = Project([employee], [Salary(employee, 900m, start: new(2029, 1, 1))], future: 1);
Check(Count(futureOnly, "MissingSalaryEmployeeCount") == 1 && futureOnly.GetProperty("EffectiveMonthlySalary").GetArrayLength() == 0,
    "future effective dates cannot inflate current monthly salary");
var zero = new CompensationRecord(employee, 1, null, 0m, 0m, "PKR", WageBasis.Monthly,
    PaymentMethod.Bank, new(2026, 1, 1), null, null, "synthetic-approver", now);
var zeroResult = Project([employee], [zero]);
Check(Count(zeroResult, "MonthlySalaryEmployeeCount") == 1 && Count(zeroResult, "MissingSalaryEmployeeCount") == 0 &&
    Amount(zeroResult.GetProperty("EffectiveMonthlySalary")[0], "GrossAmount") == 0m,
    "an explicit approved zero salary remains distinct from missing compensation");

PayrollHandoffBatch Batch(string reference, bool locked = false)
{
    var batch = new PayrollHandoffBatch(reference, new(2026, 10, 1), new(2026, 11, 1), "PKR", "synthetic-preparer", now);
    if (locked) batch.Lock(Guid.NewGuid(), "synthetic-approver", now);
    return batch;
}
PayrollHandoffLine Line(PayrollHandoffBatch batch, Guid id, decimal amount) =>
    new(batch.Id, id, PayrollComponentKind.OtherApprovedAdjustment, "synthetic.source", Guid.NewGuid(), 1,
        Guid.NewGuid(), amount, "PKR", null, null, "synthetic-private-reference", Guid.NewGuid().ToString("N"));
var prepared = Batch("synthetic-prepared");
var locked = Batch("synthetic-locked", true);
var handoff = Project([employee, second], [Salary(employee, 1000m)], batches: [prepared, locked],
    lines: [Line(prepared, employee, 200m), Line(prepared, employee, -80m), Line(locked, second, -20m)]);
var groups = handoff.GetProperty("Handoff");
Check(groups.GetArrayLength() == 2 && groups[0].GetProperty("State").GetString() == "locked" &&
    Amount(groups[0], "SignedComponentTotal") == -20m && Amount(groups[1], "SignedComponentTotal") == 120m,
    "prepared and locked handoff components retain their states and signed amounts");
Check(Count(groups[1], "EmployeeCount") == 1 && Count(groups[1], "LineCount") == 2 &&
    Count(groups[0], "LockedBatchCount") == 1 && Count(groups[1], "LockedBatchCount") == 0,
    "handoff employee, line and locked-batch counts describe actual frozen sources");
Check(Amount(handoff.GetProperty("EffectiveMonthlySalary")[0], "GrossAmount") == 1100m,
    "handoff credits and recoveries do not modify effective salary totals");
var outside = Project([employee], [], batches: [prepared], lines: [Line(prepared, second, 9999m)]);
Check(outside.GetProperty("Handoff").GetArrayLength() == 0,
    "employees outside the active server population cannot influence handoff totals or counts");

foreach (var scenario in new[] { "missing_salary", "missing_handoff", "scoped_salary", "scoped_handoff", "mobile", "no_mfa", "revoked" })
{
    var fixture = new PayrollBoundaryFixture();
    switch (scenario)
    {
        case "missing_salary": fixture.Grants.RemoveAll(x => x.Permission == EmployeeCompensationPermissions.Read); break;
        case "missing_handoff": fixture.Grants.RemoveAll(x => x.Permission == PayrollHandoffPermissions.Read); break;
        case "scoped_salary": fixture.Scope(EmployeeCompensationPermissions.Read); break;
        case "scoped_handoff": fixture.Scope(PayrollHandoffPermissions.Read); break;
        case "mobile": fixture.Identity = fixture.Identity with { Client = SessionClient.Mobile }; break;
        case "no_mfa": fixture.Identity = fixture.Identity with { MfaSatisfied = false }; break;
        case "revoked": fixture.Current = false; break;
    }
    try
    {
        await fixture.Service.SummaryAsync(fixture.Context, default);
        throw new Exception($"{scenario}: expected access_denied");
    }
    catch (AuthenticationFailure failure)
    {
        Check(failure.Code == "access_denied" && failure.StatusCode == 403,
            $"{scenario}: real EmployeeOperation denies the payroll dashboard");
    }
    Check(fixture.PopulationReads == 0 && fixture.SourceReads == 0 && fixture.Audits == 0,
        $"{scenario}: authorization denial precedes population and financial reads");
}
var authorized = new PayrollBoundaryFixture();
var empty = await authorized.Service.SummaryAsync(authorized.Context, default);
Check(empty.ActiveEmployees == 0 && empty.EffectiveMonthlySalary.Count == 0 && empty.Handoff.Count == 0 &&
    authorized.PopulationReads == 1 && authorized.SourceReads == 1 && authorized.Transactions == 1 && authorized.Audits == 1,
    "explicit All grants permit one audited operation with source-backed empty results and no nested transaction");
Console.WriteLine($"HR payroll dashboard: {checks}/{checks} passed. Synthetic aggregation/authorization only; no SQL or payment.");

public class PayrollBoundaryProxy : DispatchProxy
{
    public Func<MethodInfo, object?[], object?> Handler = null!;
    protected override object? Invoke(MethodInfo? method, object?[]? args) => Handler(method!, args ?? []);
    public static T Make<T>(Func<MethodInfo, object?[], object?> handler) where T : class
    { var proxy = Create<T, PayrollBoundaryProxy>(); ((PayrollBoundaryProxy)(object)proxy).Handler = handler; return proxy; }
}
public sealed class PayrollBoundaryClock : TimeProvider
{ public override DateTimeOffset GetUtcNow() => new(2026, 10, 19, 8, 0, 0, TimeSpan.Zero); }
public sealed class PayrollBoundaryFixture
{
    public AuthenticatedAccount Identity = new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 1, SessionClient.Web, true);
    public bool Current = true;
    public int PopulationReads, SourceReads, Transactions, Audits;
    public List<PermissionGrant> Grants;
    public AuthRequestContext Context = new("127.0.0.1", "synthetic-dashboard-payroll");
    public HrDashboardPayrollService Service;

    public PayrollBoundaryFixture()
    {
        var assignment = Guid.NewGuid();
        Grants = new[] { EmployeeCompensationPermissions.Read, PayrollHandoffPermissions.Read }
            .Select(code => new PermissionGrant(code, true, assignment, AccessScopeKind.All, null)).ToList();
        var current = PayrollBoundaryProxy.Make<ICurrentAuthentication>((method, _) => method.Name == "Require"
            ? Identity : throw new Exception(method.Name));
        var access = PayrollBoundaryProxy.Make<IAccountAccessReader>((method, _) => method.Name == "GetGrantsAsync"
            ? Task.FromResult<IReadOnlyList<PermissionGrant>>(Grants.ToArray()) : throw new Exception(method.Name));
        var authorization = PayrollBoundaryProxy.Make<IAccessManagementRepository>((method, args) =>
        {
            switch (method.Name)
            {
                case "InTransactionAsync": Transactions++; return ((Delegate)args[0]!).DynamicInvoke(args[1]);
                case "IsCurrentAdministratorAsync": return Task.FromResult(Current);
                case "GetAssignmentRoleCodeAsync": return Task.FromResult<string?>("SYNTHETIC_HR");
                default: throw new Exception(method.Name);
            }
        });
        var hiring = PayrollBoundaryProxy.Make<IEmployeeHiringRepository>((method, _) =>
        {
            switch (method.Name)
            {
                case "AddAudit": Audits++; return null;
                case "SaveChangesAsync": return Task.CompletedTask;
                default: throw new Exception(method.Name);
            }
        });
        var files = PayrollBoundaryProxy.Make<IPrivateFileStore>((method, _) => throw new Exception("Unexpected private IO: " + method.Name));
        var operation = new EmployeeOperation(current, access, authorization, hiring, new PayrollBoundaryClock(), files);
        var population = PayrollBoundaryProxy.Make<IHrDashboardPopulationRepository>((method, _) =>
        {
            PopulationReads++;
            return method.Name == "ActiveEmployeeIdsAsync" ? Task.FromResult<IReadOnlyList<Guid>>([]) : throw new Exception(method.Name);
        });
        var compensation = PayrollBoundaryProxy.Make<IEmployeeCompensationRepository>((method, _) =>
            throw new Exception("Unexpected compensation read for empty population: " + method.Name));
        var repository = PayrollBoundaryProxy.Make<IHrDashboardPayrollRepository>((method, _) =>
        {
            SourceReads++;
            return method.Name == "ReadAsync" ? Task.FromResult(new HrDashboardPayrollSources(0, 0, [], [])) : throw new Exception(method.Name);
        });
        Service = new(operation, population, compensation, repository);
    }
    public void Scope(string permission) => Grants = Grants.Select(x => x.Permission == permission
        ? x with { ScopeKind = AccessScopeKind.Employee, TargetId = Identity.EmployeeId } : x).ToList();
}
