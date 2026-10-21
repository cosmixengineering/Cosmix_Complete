using System.Collections;
using System.Data.Common;
using System.Linq.Expressions;
using System.Reflection;
using Cosmix.Application.HR.Leaves;
using Cosmix.Application.HR.VehicleFuel;
using Cosmix.Domain.Common.Approvals;
using Cosmix.Domain.Common.Audit;
using Cosmix.Domain.Common.Idempotency;
using Cosmix.Domain.HR.Leaves;
using Cosmix.Domain.HR.VehicleFuel;
using Cosmix.Infrastructure.Database;
using Cosmix.Infrastructure.Repositories.HR;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Query;

// Capture actual repository expressions through synthetic enumeration, including later queries
// after counts/initial reads. Compile each unchanged expression with the real SQL Server provider.
var guard = new VehicleBenefitConnectionGuard();
const string connection = "Server=never-connect.invalid;Database=OfflineOnly;Integrated Security=true;Encrypt=true";
using var memory = new CosmixDbContext(new DbContextOptionsBuilder<CosmixDbContext>().UseSqlServer(connection)
    .ReplaceService<IAsyncQueryProvider, VehicleBenefitCaptureProvider>().AddInterceptors(guard).Options);
using var sql = new CosmixDbContext(new DbContextOptionsBuilder<CosmixDbContext>()
    .UseSqlServer(connection).AddInterceptors(guard).Options);
var vehicles = new VehicleFuelRepository(memory);
var benefits = new LeaveBenefitRepository(memory);
var employee = Guid.Parse("a0000000-0000-4000-8000-000000000001");
var vehicle = Guid.Parse("a0000000-0000-4000-8000-000000000002");
var id = Guid.Parse("a0000000-0000-4000-8000-000000000003");
var date = new DateOnly(2026, 10, 1);
var from = new DateTimeOffset(2026, 10, 1, 0, 0, 0, TimeSpan.Zero);
var to = from.AddDays(2);
var failures = new List<string>();
var passed = 0;
var queries = 0;
var sqlStrings = 0;
var token = CancellationToken.None;

async Task Check(string name, Func<Task> body)
{
    try { await body(); passed++; Console.WriteLine("PASS " + name); }
    catch (Exception error)
    {
        failures.Add(name);
        Console.WriteLine("FAIL " + name + ": " + error.GetBaseException().Message);
    }
}
async Task CompileScalar(Expression expression, Type resultType)
{
    var provider = (IAsyncQueryProvider)((IQueryable)sql.Vehicles).Provider;
    var method = typeof(IAsyncQueryProvider).GetMethod(nameof(IAsyncQueryProvider.ExecuteAsync))!
        .MakeGenericMethod(resultType);
    var before = guard.Blocked;
    try
    {
        Task task;
        try { task = (Task)method.Invoke(provider, [expression, token])!; }
        catch (TargetInvocationException error) { throw error.InnerException!; }
        await task;
        throw new Exception("Scalar unexpectedly completed without the connection guard.");
    }
    catch (VehicleBenefitConnectionBlocked)
    {
        if (guard.Blocked != before + 1) throw new Exception("Unexpected guard count.");
    }
}
async Task Scenario(string name, Func<Task> action, Action? seed = null)
{
    await Check(name, async () =>
    {
        VehicleBenefitSynthetic.Clear();
        VehicleBenefitCaptureProvider.Captured.Clear();
        seed?.Invoke();
        await action();
        var captures = VehicleBenefitCaptureProvider.Captured.ToArray();
        if (captures.Length == 0) throw new Exception("No actual repository expression captured.");
        foreach (var capture in captures)
        {
            queries++;
            if (capture.ResultType is not null)
            {
                // Preserve the terminal operator AND its predicate, unlike checking only Arguments[0].
                await CompileScalar(capture.Expression, capture.ResultType);
            }
            else
            {
                var text = ((IQueryable)sql.Vehicles).Provider.CreateQuery(capture.Expression).ToQueryString();
                if (!text.Contains("SELECT", StringComparison.OrdinalIgnoreCase))
                    throw new Exception("No SQL SELECT generated.");
                sqlStrings++;
            }
        }
        Console.WriteLine("  actual captured queries=" + captures.Length);
    });
}

await Scenario("vehicle lookup", () => vehicles.FindVehicleAsync(vehicle, token));
await Scenario("registration lookup normalization", () => vehicles.FindVehicleByRegistrationAsync("synthetic-001", token));
await Scenario("active vehicle list", () => vehicles.ListVehiclesAsync(true, token));
await Scenario("all vehicle list", () => vehicles.ListVehiclesAsync(false, token));
await Scenario("custody detail includes", () => vehicles.FindCustodyAsync(id, token));
await Scenario("active vehicle custody", () => vehicles.FindActiveCustodyForVehicleAsync(vehicle, token));
await Scenario("active employee custody", () => vehicles.FindActiveCustodyForEmployeeAsync(employee, token));
await Scenario("filtered custody history", () => vehicles.ListCustodyHistoryAsync(vehicle, employee, token));
await Scenario("custody overlap Any predicate", () => vehicles.HasCustodyPeriodOverlapAsync(vehicle, employee, from, token));
await Scenario("custody overlapping interval", () => vehicles.FindCustodiesOverlappingAsync(vehicle, from, to, token));
foreach (var returned in new DateTimeOffset?[] { null, to, to.AddHours(1) })
    await Scenario("custody return conflict all three queries: " + returned?.ToString("O"),
        () => vehicles.HasCustodyReturnConflictAsync(id, to, 500m, token), () =>
            VehicleBenefitSynthetic.Add<VehicleCustody>(("Id", id), ("VehicleId", vehicle), ("EmployeeId", employee),
                ("IssuedAt", from), ("ReturnedAt", returned)));
await Scenario("dated commute", () => vehicles.FindActiveCommuteDistanceAsync(employee, date, token));
foreach (var end in new DateOnly?[] { null, date.AddDays(10) })
    await Scenario("commute overlap " + end, () => vehicles.HasCommutePeriodOverlapAsync(employee, date, end, token));
foreach (var excluded in new Guid?[] { null, id })
    await Scenario("trip time and odometer overlap " + excluded,
        () => vehicles.HasTripOverlapAsync(vehicle, from, to, 100, 200, excluded, token));
await Scenario("trip detail includes", () => vehicles.FindTripAsync(id, token));
await Scenario("filtered trip page count and rows", () => vehicles.PageTripsAsync(employee, vehicle, ApprovalState.Approved, 25, 25, token));
await Scenario("unfiltered trip page count and rows", () => vehicles.PageTripsAsync(null, null, null, 0, 25, token));
await Scenario("fuel detail includes", () => vehicles.FindFuelClaimAsync(id, token));
await Scenario("fuel claim existence predicate", () => vehicles.HasClaimForTripAsync(id, token));
await Scenario("filtered fuel page count and rows", () => vehicles.PageFuelClaimsAsync(employee, ApprovalState.Pending, 25, 25, token));
await Scenario("service detail includes", () => vehicles.FindServiceRecordAsync(id, token));
await Scenario("exact approved mileage decision", () => vehicles.FindApprovedMileageForTripAsync(id, token));
await Scenario("approved mileage Sum full scalar", () => vehicles.GetAccumulatedOutstationMileageAsync(vehicle, date, token));
await Scenario("approved mileage dated list", () => vehicles.ListApprovedMileageAsync(vehicle, date, token));
await Scenario("latest approved service with decision Exists", () => vehicles.FindLatestApprovedServiceAsync(vehicle, VehicleServiceKind.Tuning, token));
await Scenario("service evidence Any predicate", () => vehicles.HasServiceEvidenceAsync(vehicle, "synthetic-proof", token));
await Scenario("filtered service page count and rows", () => vehicles.PageServiceRecordsAsync(vehicle, employee, ApprovalState.Approved, 25, 25, token));
await Scenario("incident detail nested includes", () => vehicles.FindIncidentAsync(id, token));
await Scenario("own incident count and page", () => vehicles.PageOwnIncidentsAsync(employee, 25, 25, token));
await Scenario("filtered incident count and page", () => vehicles.PageIncidentsAsync(id, ApprovalState.Pending, 25, 25, token));
await Scenario("exact incident decision", () => vehicles.FindIncidentDecisionAsync(id, token));
await Scenario("active authorized policy selection", () => vehicles.GetActiveVehicleFuelPolicyVersionAsync(date, token));
await Scenario("retained authorized policy", () => vehicles.FindVehicleFuelPolicyVersionAsync(id, token));
await Scenario("policy page rows and count", () => vehicles.PagePoliciesAsync(25, 25, token));
await Scenario("policy detail", () => vehicles.FindPolicyAsync(id, token));
await Scenario("policy code existence", () => vehicles.PolicyCodeExistsAsync("synthetic-policy", token));
await Scenario("policy version page rows and count", () => vehicles.PagePolicyVersionsAsync(id, 25, 25, token));
await Scenario("policy version detail", () => vehicles.FindPolicyVersionAsync(vehicle, id, token));
await Scenario("policy version Max full scalar", () => vehicles.LatestPolicyVersionNumberAsync(id, token));
await Scenario("policy overlap history", () => vehicles.HasPolicyPeriodOverlapAsync(date, date.AddDays(10), token));
await Scenario("policy employee Contains", () => vehicles.ExistingPolicyEmployeesAsync([employee, id], token));
await Scenario("policy role Contains", () => vehicles.ActivePolicyRoleCodesAsync(["HR", "ADMIN"], token));
await Scenario("retained commute ID", () => vehicles.FindCommuteDistanceByIdAsync(id, token));
await Scenario("adjustment history ordered and bounded", () => vehicles.ListVehicleAdjustmentRequestsAsync(id, VehicleFuelService.FuelReconciliationRequestType, token));
await Scenario("tracked adjustment request", () => vehicles.FindVehicleAdjustmentRequestAsync(id, VehicleFuelService.FuelReconciliationRequestType, token));
await Scenario("unique adjustment decision", () => vehicles.FindVehicleAdjustmentDecisionAsync(id, token));

void SeedBenefit() => VehicleBenefitSynthetic.Add<LeaveLedgerEntry>(("Id", id), ("EmployeeId", employee),
    ("LeaveYearStart", date), ("LeaveYearEnd", date.AddYears(1)), ("EntryType", LeaveLedgerEntryType.Entitlement),
    ("MinutesDelta", 480), ("RecordedAt", from));
await Scenario("benefit year and nonempty audit Attach", () => benefits.YearAsync(employee, date, date.AddYears(1), token), SeedBenefit);
await Scenario("benefit exact ID and nonempty audit Attach", () => benefits.FindAsync(employee, id, token), SeedBenefit);
await Scenario("benefit year and empty audit Attach", () => benefits.YearAsync(employee, date, date.AddYears(1), token));
await Scenario("benefit source key Any predicate", () => benefits.SourceExistsAsync(employee, "synthetic-source", token));
await Scenario("benefit active recipient projection Single", () => benefits.RecipientAsync(employee, token));
await Scenario("benefit usage entity and policy projection", () => benefits.UsageAsync(employee, date, date.AddYears(1), token));

// Transaction-gated resolution methods are not invoked with fabricated authorization.
// These controls compile their source query shapes only, not their transaction/authority guards.
await Check("resolution facts source-query control", () =>
{
    var target = id.ToString();
    var text = sql.Set<AuditEvent>().AsNoTracking().Where(x => x.EmployeeScopeId == employee &&
        x.TargetType == LeaveBenefitEvidence.ResolutionTargetType && x.TargetId == target)
        .OrderBy(x => x.OccurredAt).ThenBy(x => x.Id).Take(2).ToQueryString();
    if (!text.Contains("[EmployeeScopeId]") || !text.Contains("TOP") || !text.Contains("ORDER BY"))
        throw new Exception("Missing resolution bounds.");
    sqlStrings++;
    return Task.CompletedTask;
});
await Check("resolution receipt source-query control", async () =>
{
    var actor = employee.ToString();
    var before = guard.Blocked;
    try
    {
        await sql.Set<IdempotencyReceipt>().AsNoTracking().SingleOrDefaultAsync(x => x.ActorUserId == actor &&
            x.Operation == LeaveBenefitService.ResolutionOperation && x.Key == "synthetic-resolution", token);
        throw new Exception("Expected blocked connection.");
    }
    catch (VehicleBenefitConnectionBlocked)
    {
        if (guard.Blocked != before + 1) throw new Exception("Unexpected guard count.");
    }
});
await Check("negative control rejects record projection followed by predicate", () =>
{
    try
    {
        _ = sql.Vehicles.Select(v => new VehicleBenefitProjection(v)).Where(r => r.Vehicle.Id == vehicle)
            .Take(2).ToQueryString();
    }
    catch (InvalidOperationException error) when (error.Message.Contains("could not be translated", StringComparison.Ordinal))
    { return Task.CompletedTask; }
    throw new Exception("Negative calibration did not reproduce the known projection translation failure.");
});
await Check("positive control filters before final record projection", () =>
{
    var text = sql.Vehicles.Where(v => v.Id == vehicle).Take(2).Select(v => new VehicleBenefitProjection(v)).ToQueryString();
    if (!text.Contains("WHERE") || !text.Contains("[Id]")) throw new Exception("Missing SQL scope.");
    sqlStrings++;
    return Task.CompletedTask;
});
if (guard.Opened != 0) throw new Exception("Unexpected opened connection.");
Console.WriteLine($"Vehicle/benefit translation: {passed}/{passed + failures.Count}; actual repository expressions={queries}; " +
    $"ToQueryString={sqlStrings}; intercepted pre-open scalar compilations={guard.Blocked}; opened connections={guard.Opened}.");
Console.WriteLine("No SQL executed, no host/private storage; resolution controls are source shapes only.");
return failures.Count == 0 ? 0 : 1;

sealed record VehicleBenefitProjection(Vehicle Vehicle);
sealed class VehicleBenefitConnectionBlocked : Exception { }
sealed class VehicleBenefitConnectionGuard : DbConnectionInterceptor
{
    public int Blocked;
    public int Opened;
    public override InterceptionResult ConnectionOpening(DbConnection connection, ConnectionEventData data, InterceptionResult result)
    { Blocked++; throw new VehicleBenefitConnectionBlocked(); }
    public override ValueTask<InterceptionResult> ConnectionOpeningAsync(DbConnection connection, ConnectionEventData data,
        InterceptionResult result, CancellationToken cancellationToken = default)
    { Blocked++; throw new VehicleBenefitConnectionBlocked(); }
    public override void ConnectionOpened(DbConnection connection, ConnectionEndEventData data)
    { Opened++; throw new Exception("Connection opened unexpectedly."); }
    public override Task ConnectionOpenedAsync(DbConnection connection, ConnectionEndEventData data, CancellationToken cancellationToken = default)
    { Opened++; throw new Exception("Connection opened unexpectedly."); }
}
static class VehicleBenefitSynthetic
{
    private static readonly Dictionary<Type, IList> data = [];
    public static void Clear() => data.Clear();
    public static void Add<T>(params (string Name, object? Value)[] fields) where T : class
    {
        var row = (T)Activator.CreateInstance(typeof(T), true)!;
        foreach (var (name, value) in fields) typeof(T).GetProperty(name)!.SetValue(row, value);
        if (!data.TryGetValue(typeof(T), out var rows)) data[typeof(T)] = rows = new List<T>();
        rows.Add(row);
    }
    public static IQueryable Rows(Type type)
    {
        if (!data.TryGetValue(type, out var rows)) rows = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(type))!;
        return (IQueryable)typeof(Queryable).GetMethods().Single(m => m.Name == "AsQueryable" && m.IsGenericMethod)
            .MakeGenericMethod(type).Invoke(null, [rows])!;
    }
}
public sealed class VehicleBenefitCaptureProvider : IAsyncQueryProvider
{
    public static List<(Expression Expression, Type? ResultType)> Captured { get; } = [];
    public IQueryable CreateQuery(Expression expression) =>
        (IQueryable)Activator.CreateInstance(typeof(VehicleBenefitCaptureQuery<>).MakeGenericType(expression.Type.GetGenericArguments()[0]), this, expression)!;
    public IQueryable<T> CreateQuery<T>(Expression expression) => new VehicleBenefitCaptureQuery<T>(this, expression);
    public object? Execute(Expression expression) => Expression.Lambda(new SyntheticRoots().Visit(expression)!).Compile().DynamicInvoke();
    public T Execute<T>(Expression expression) => (T)Execute(expression)!;
    public T ExecuteAsync<T>(Expression expression, CancellationToken cancellationToken = default)
    {
        Captured.Add((expression, typeof(T)));
        if (!typeof(T).IsGenericType || typeof(T).GetGenericTypeDefinition() != typeof(Task<>))
            throw new Exception("Unexpected scalar result type.");
        return (T)typeof(Task).GetMethod(nameof(Task.FromResult))!.MakeGenericMethod(typeof(T).GetGenericArguments()[0])
            .Invoke(null, [Execute(expression)])!;
    }
    private sealed class SyntheticRoots : ExpressionVisitor
    {
        protected override Expression VisitExtension(Expression node) =>
            node is EntityQueryRootExpression root ? VehicleBenefitSynthetic.Rows(root.EntityType.ClrType).Expression : base.VisitExtension(node);
        protected override Expression VisitMethodCall(MethodCallExpression node) =>
            node.Method.DeclaringType == typeof(EntityFrameworkQueryableExtensions) &&
            node.Method.Name is "AsNoTracking" or "Include" or "ThenInclude" ? Visit(node.Arguments[0]) : base.VisitMethodCall(node);
    }
}
public sealed class VehicleBenefitCaptureQuery<T>(VehicleBenefitCaptureProvider provider, Expression expression) : IOrderedQueryable<T>, IAsyncEnumerable<T>
{
    public Type ElementType => typeof(T);
    public Expression Expression => expression;
    public IQueryProvider Provider => provider;
    public IEnumerator<T> GetEnumerator() => provider.Execute<IEnumerable<T>>(expression).GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    public async IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken cancellationToken = default)
    {
        VehicleBenefitCaptureProvider.Captured.Add((expression, null));
        await Task.CompletedTask;
        foreach (var item in this) { cancellationToken.ThrowIfCancellationRequested(); yield return item; }
    }
}
