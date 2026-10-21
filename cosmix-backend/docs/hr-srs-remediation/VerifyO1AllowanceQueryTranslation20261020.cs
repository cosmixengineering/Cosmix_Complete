using System.Data;
using System.Data.Common;
using System.Reflection;
using Cosmix.Application.HR.Allowances;
using Cosmix.Domain.Common.Approvals;
using Cosmix.Domain.HR.Allowances;
using Cosmix.Infrastructure.Database;
using Cosmix.Infrastructure.Repositories.Attendance;
using Cosmix.Infrastructure.Repositories.HR;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

// Actual compiled allowance queries only. No DeploymentRepository changes/tests:
// that surface belongs to the independent deployment-query verifier.
var blocker = new AllowanceTranslationConnectionGuard();
var commands = new AllowanceTranslationCommandGuard();
using var db = new CosmixDbContext(new DbContextOptionsBuilder<CosmixDbContext>()
    .UseSqlServer("Server=offline.invalid;Database=NeverConnect;Integrated Security=true;Encrypt=true")
    .AddInterceptors(blocker, commands).Options);
var repository = new AllowanceRepository(db, new OvertimeRepository(db));
var employee = Guid.Parse("35d19922-70a8-4c80-8578-e1029360e9fc");
var id = Guid.Parse("20102001-0008-4000-8000-000000000008");
var start = new DateTimeOffset(2026, 10, 18, 4, 0, 0, TimeSpan.Zero);
var end = new DateTimeOffset(2026, 10, 19, 12, 0, 0, TimeSpan.Zero);
var first = new DateOnly(2026, 10, 18);
var passes = 0;
var failures = new List<string>();
void Check(bool result, string name)
{
    if (result) { passes++; Console.WriteLine("PASS " + name); }
    else { failures.Add(name); Console.WriteLine("FAIL " + name); }
}
async Task Compile(string name, Func<Task> action)
{
    var blockedBefore = blocker.Blocked;
    try { await action(); Check(false, name + " unexpectedly completed"); }
    catch (AllowanceTranslationBlocked)
    { Check(blocker.Blocked == blockedBefore + 1, name + " translated; connection attempt intercepted before opening"); }
    catch (InvalidOperationException error)
    { Check(false, name + ": " + error.Message); }
}
IQueryable<T> Query<T>(string method, params object[] args) =>
    (IQueryable<T>)typeof(AllowanceRepository).GetMethod(method, BindingFlags.NonPublic | BindingFlags.Instance)!
        .Invoke(repository, args)!;
void Translate(string name, IQueryable query, params string[] fragments)
{
    try
    {
        var sql = query.ToQueryString();
        Check(sql.Contains("SELECT", StringComparison.OrdinalIgnoreCase) &&
            fragments.All(x => sql.Contains(x, StringComparison.OrdinalIgnoreCase)), name);
    }
    catch (InvalidOperationException error) { Check(false, name + ": " + error.Message); }
}

// Direct post-deployment dependencies: Employment -> real OvertimeRepository;
// Calculate -> resolver -> SourceAsync (membership read then candidates).
foreach (var date in new[] { first, first.AddDays(1), first.AddDays(2) })
{
    await Compile("EmploymentAsync " + date, () => repository.EmploymentAsync(employee, date, default));
    await Compile("SourceAsync membership stage " + date, () => repository.SourceAsync(employee, date, default));
    Translate("SourceAsync actual candidate stage " + date,
        Query<AllowancePolicyCandidate>("ApplicableQuery", employee, date)
            .OrderBy(x => x.Assignment.Id).Take(129),
        "TOP", "[EmployeeId]", "[EffectiveFrom]", "[EffectiveTo]", "[Category]", "[Authority]", "ORDER BY");
}

// Generic allowance reads/review are adjacent consumers of retained outstation
// claims. These are not substitutes for DeploymentRepository.ClaimsAsync.
await Compile("FindAsync claim and employee filter", () => repository.FindAsync(employee, id, default));
await Compile("TrackApprovalEvidenceAsync", () => repository.TrackApprovalEvidenceAsync(id, default));
await Compile("PageAsync employee and date/state/requester filters", () =>
    repository.PageAsync(new(employee, first, first.AddDays(2), ApprovalState.Pending, 0, 25), employee, default));
await Compile("PageAsync unfiltered later page", () =>
    repository.PageAsync(new(null, null, null, null, 25, 25), null, default));
await Compile("DeploymentClaimOverlapAsync", () => repository.DeploymentClaimOverlapAsync(employee, start, end, default));
await Compile("KeyClaimExistsAsync", () => repository.KeyClaimExistsAsync(employee, AllowanceKind.OutstationDaily, "OFFLINE-ONLY", default));
await Compile("DateClaimExistsAsync", () => repository.DateClaimExistsAsync(employee, AllowanceKind.OutstationDaily, first, default));
await Compile("KindsClaimedAsync", () => repository.KindsClaimedAsync(employee, first, default));

foreach (var tracked in new[] { false, true })
{
    var records = Query<AllowanceRecord>("Records", tracked);
    Translate("actual Records filtered after member-init, tracked=" + tracked,
        records.Where(x => x.Claim.Id == id && x.Claim.EmployeeId == employee).Take(2),
        "TOP", "[EmployeeId]", "[RequestType]", "[SubjectId]", "[SnapshotPrivateReference]", "[DecidedAt]");
    Translate("actual Records ordered after member-init, tracked=" + tracked,
        records.Where(x => x.Claim.EmployeeId == employee && x.Approval.State == ApprovalState.Pending)
            .OrderByDescending(x => x.Claim.CreatedAt).ThenBy(x => x.Claim.Id).Skip(25).Take(25),
        "[EmployeeId]", "[State]", "ORDER BY", "OFFSET", "FETCH NEXT");
}
// Same underlying policy projection is also filtered/ordered by management
// reads. Test the existing method, without exercising publication or writes.
await Compile("AssignmentsAsync filtered member-init", () => repository.AssignmentsAsync(id, default));
Translate("actual AssignmentQuery bounded ordered tail",
    Query<AllowancePolicyCandidate>("AssignmentQuery").Where(x => x.Version.PolicyId == id)
        .OrderBy(x => x.Assignment.EffectiveFrom).ThenBy(x => x.Assignment.Id).Take(4097),
    "TOP", "[PolicyId]", "[EffectiveFrom]", "[Authority]", "ORDER BY");

// Deliberately unsupported constructor projection is a negative control for
// the exact failure class; it must fail before the connection guard is reached.
var opensBeforeNegative = blocker.Blocked;
try
{
    _ = db.AllowanceClaims.Select(x => new ConstructorProjectionControl(x))
        .Where(x => x.Claim.EmployeeId == employee).ToQueryString();
    Check(false, "constructor-before-filter negative control unexpectedly translated");
}
catch (InvalidOperationException error)
{
    Check(error.Message.Contains("could not be translated", StringComparison.OrdinalIgnoreCase),
        "constructor-before-filter negative control rejected by SQL translator");
}
Check(blocker.Blocked == opensBeforeNegative, "negative control never reached connection opening");
Check(blocker.Opened == 0 && db.Database.GetDbConnection().State == ConnectionState.Closed,
    "zero opened connections; connection remains closed");
Check(commands.Attempts == 0, "zero executed SQL commands");
Check(!db.ChangeTracker.Entries().Any(), "no materialized or staged business rows");
Console.WriteLine($"VerifyO1AllowanceQueryTranslation20261020: {passes}/{passes + failures.Count} passed; {failures.Count} failed; " +
    $"blocked opens={blocker.Blocked}; opened={blocker.Opened}; SQL commands={commands.Attempts}.");
Console.WriteLine("Translation only, not database/data correctness: SourceAsync stage two tested separately through actual ApplicableQuery. No host, SQL execution, private storage, writes or migrations.");
if (failures.Count != 0) Environment.Exit(1);

sealed record ConstructorProjectionControl(AllowanceClaim Claim);
sealed class AllowanceTranslationBlocked : Exception { }
sealed class AllowanceTranslationConnectionGuard : DbConnectionInterceptor
{
    public int Blocked, Opened;
    public override InterceptionResult ConnectionOpening(DbConnection connection, ConnectionEventData data, InterceptionResult result)
    { Blocked++; throw new AllowanceTranslationBlocked(); }
    public override ValueTask<InterceptionResult> ConnectionOpeningAsync(DbConnection connection, ConnectionEventData data,
        InterceptionResult result, CancellationToken ct = default)
    { Blocked++; throw new AllowanceTranslationBlocked(); }
    public override void ConnectionOpened(DbConnection connection, ConnectionEndEventData data)
    { Opened++; throw new InvalidOperationException("Offline connection unexpectedly opened."); }
    public override Task ConnectionOpenedAsync(DbConnection connection, ConnectionEndEventData data, CancellationToken ct = default)
    { Opened++; throw new InvalidOperationException("Offline connection unexpectedly opened."); }
}
sealed class AllowanceTranslationCommandGuard : DbCommandInterceptor
{
    public int Attempts;
    public override InterceptionResult<DbDataReader> ReaderExecuting(DbCommand command, CommandEventData data, InterceptionResult<DbDataReader> result)
    { Attempts++; throw new InvalidOperationException("No SQL permitted."); }
    public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData data,
        InterceptionResult<DbDataReader> result, CancellationToken ct = default)
    { Attempts++; throw new InvalidOperationException("No SQL permitted."); }
    public override InterceptionResult<object> ScalarExecuting(DbCommand command, CommandEventData data, InterceptionResult<object> result)
    { Attempts++; throw new InvalidOperationException("No SQL permitted."); }
    public override ValueTask<InterceptionResult<object>> ScalarExecutingAsync(DbCommand command, CommandEventData data,
        InterceptionResult<object> result, CancellationToken ct = default)
    { Attempts++; throw new InvalidOperationException("No SQL permitted."); }
    public override InterceptionResult<int> NonQueryExecuting(DbCommand command, CommandEventData data, InterceptionResult<int> result)
    { Attempts++; throw new InvalidOperationException("No SQL permitted."); }
    public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(DbCommand command, CommandEventData data,
        InterceptionResult<int> result, CancellationToken ct = default)
    { Attempts++; throw new InvalidOperationException("No SQL permitted."); }
}
