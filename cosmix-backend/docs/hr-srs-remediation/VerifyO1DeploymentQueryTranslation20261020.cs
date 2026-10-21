using System.Data.Common;
using System.Reflection;
using Cosmix.Application.HR.Allowances;
using Cosmix.Application.Attendance.Overtime;
using Cosmix.Domain.Common.Approvals;
using Cosmix.Domain.HR.Allowances;
using Cosmix.Infrastructure.Database;
using Cosmix.Infrastructure.Repositories.Attendance;
using Cosmix.Infrastructure.Repositories.HR;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

// Offline SQL Server query compilation only. No actual connection, host or private storage.
var guard = new O1TranslationNoConnection();
using var db = new CosmixDbContext(new DbContextOptionsBuilder<CosmixDbContext>()
    .UseSqlServer("Server=not-used.invalid;Database=NeverConnect;Integrated Security=true;TrustServerCertificate=true")
    .AddInterceptors(guard).Options);
var repository = new DeploymentRepository(db);
var employee = Guid.Parse("35d19922-70a8-4c80-8578-e1029360e9fc");
var id = Guid.Parse("40102001-0001-4000-8000-000000000001");
var from = new DateTimeOffset(2026, 10, 18, 4, 0, 0, TimeSpan.Zero);
var to = new DateTimeOffset(2026, 10, 19, 12, 0, 0, TimeSpan.Zero);
var date = new DateOnly(2026, 10, 18);
var failures = new List<string>();
var passes = 0;
void Translate(string name, IQueryable query, params string[] expectedSql)
{
    try
    {
        var sql = query.ToQueryString();
        if (!sql.Contains("SELECT", StringComparison.OrdinalIgnoreCase)) throw new Exception("No SELECT produced.");
        if (expectedSql.Any(fragment => !sql.Contains(fragment, StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException("Expected SQL scope, bound or ordering fragment is missing.");
        passes++;
        Console.WriteLine("PASS " + name);
    }
    catch (InvalidOperationException e)
    {
        failures.Add(name);
        Console.WriteLine("FAIL " + name + ": " + e.Message);
    }
}

// Execute the actual repository method only as far as provider compilation.
// The interceptor throws before connection opening if translation succeeds.
async Task CompileMethod(string name, Func<Task> action)
{
    var before = guard.Blocked;
    try
    {
        await action();
        throw new Exception("Repository unexpectedly completed without the connection guard.");
    }
    catch (O1TranslationConnectionBlocked)
    {
        if (guard.Blocked != before + 1) throw new Exception("Connection guard invocation count changed.");
        passes++;
        Console.WriteLine("PASS " + name + " translated; connection opening blocked");
    }
    catch (InvalidOperationException e)
    {
        failures.Add(name);
        Console.WriteLine("FAIL " + name + ": " + e.Message);
    }
}
await CompileMethod("actual FindAsync", () => repository.FindAsync(employee, id, default));
await CompileMethod("actual FindByApprovalAsync", () => repository.FindByApprovalAsync(employee, id, default));
await CompileMethod("actual PageAsync first page", () => repository.PageAsync(employee, 0, 25, default));
await CompileMethod("actual PageAsync later page", () => repository.PageAsync(employee, 25, 25, default));
await CompileMethod("actual OverlapAsync", () => repository.OverlapAsync(employee, from, to, default));
await CompileMethod("actual BreaksAsync", () => repository.BreaksAsync(id, default));
await CompileMethod("actual ClaimsAsync", () => repository.ClaimsAsync(id, default));
await CompileMethod("actual MealOverlapAsync", () => repository.MealOverlapAsync(employee, from, to, default));

// Positive SQL controls retain the employee, half-open interval, approval state and pagination.
var candidates = from d in db.Set<OutstationDeployment>()
                 join a in db.ApprovalRequests on d.ApprovalRequestId equals a.Id
                 select new { Deployment = d, Approval = a };
Translate("control entity-join overlap predicate", db.Employees.Select(_ =>
    candidates.Any(x => x.Deployment.EmployeeId == employee && x.Deployment.StartsAtUtc < to &&
        x.Deployment.EndsAtUtc > from && (x.Approval.State == ApprovalState.Pending || x.Approval.State == ApprovalState.Approved))).Take(1),
    "EXISTS", "[EmployeeId]", "[StartsAtUtc] <", "[EndsAtUtc] >", "[State]");
Translate("control filter before final record projection",
    candidates.Where(x => x.Deployment.Id == id && x.Deployment.EmployeeId == employee).Take(2)
        .Select(x => new DeploymentRow(x.Deployment, x.Approval,
            db.ApprovalDecisions.Where(d => d.ApprovalRequestId == x.Approval.Id).OrderBy(d => d.DecidedAt).FirstOrDefault())),
    "TOP", "[EmployeeId]", "[DecidedAt]");
Translate("control employee-scoped deterministic pagination before projection",
    candidates.Where(x => x.Deployment.EmployeeId == employee).OrderByDescending(x => x.Deployment.RecordedAt)
        .ThenBy(x => x.Deployment.Id).Skip(25).Take(25)
        .Select(x => new DeploymentRow(x.Deployment, x.Approval,
            db.ApprovalDecisions.Where(d => d.ApprovalRequestId == x.Approval.Id).OrderBy(d => d.DecidedAt).FirstOrDefault())),
    "[EmployeeId]", "ORDER BY", "[RecordedAt] DESC", "OFFSET", "FETCH NEXT");
Translate("employment query used before overlap",
    (from assignment in db.EmployeeAssignments.AsNoTracking()
     join category in db.StaffCategories.AsNoTracking() on assignment.StaffCategoryId equals category.Id
     where assignment.EmployeeId == employee && assignment.EffectiveFrom <= date &&
         (assignment.EffectiveTo == null || date < assignment.EffectiveTo)
     select new OvertimeEmploymentSource(assignment.Id, category.Id, category.Code, assignment.SiteId,
         assignment.EffectiveFrom, assignment.EffectiveTo, assignment.BranchId)).Take(2));
var allowances = new AllowanceRepository(db, new OvertimeRepository(db));
var records = (IQueryable<AllowanceRecord>)typeof(AllowanceRepository).GetMethod("Records",
    BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(allowances, [true])!;
Translate("allowance member-initializer projection + predicate",
    records.Where(x => x.Claim.Id == id && x.Claim.EmployeeId == employee).Take(2));
var applicable = (IQueryable<AllowancePolicyCandidate>)typeof(AllowanceRepository).GetMethod("ApplicableQuery",
    BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(allowances, [employee, date])!;
Translate("allowance applicable policy query", applicable.OrderBy(x => x.Assignment.Id).Take(129));

if (guard.Blocked != 8 || db.Database.GetDbConnection().State != System.Data.ConnectionState.Closed)
{
    failures.Add("SQL connection guard count/state");
    Console.WriteLine($"FAIL SQL connection guard: expected exactly 8 blocked opens and Closed state; got {guard.Blocked} and {db.Database.GetDbConnection().State}.");
}
else
{
    passes++;
    Console.WriteLine("PASS SQL connection guard: exactly 8 blocked opens; connection remains Closed");
}
Console.WriteLine($"Translation checks: {passes} passed; {failures.Count} failed; blocked opens={guard.Blocked}; actual opened connections=0.");
if (failures.Count != 0) throw new Exception("O1 query translation regression: " + string.Join("; ", failures));

public sealed class O1TranslationConnectionBlocked : Exception { }
public sealed class O1TranslationNoConnection : DbConnectionInterceptor
{
    public int Blocked;
    public override InterceptionResult ConnectionOpening(DbConnection connection, ConnectionEventData eventData, InterceptionResult result)
    { Blocked++; throw new O1TranslationConnectionBlocked(); }
    public override ValueTask<InterceptionResult> ConnectionOpeningAsync(DbConnection connection, ConnectionEventData eventData,
        InterceptionResult result, CancellationToken cancellationToken = default)
    { Blocked++; throw new O1TranslationConnectionBlocked(); }
}
