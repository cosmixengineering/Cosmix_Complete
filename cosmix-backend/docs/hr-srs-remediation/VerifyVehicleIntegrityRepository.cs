using System.Collections;
using System.Data.Common;
using System.Linq.Expressions;
using Cosmix.Application.Authentication;
using Cosmix.Domain.Common.Approvals;
using Cosmix.Domain.HR.VehicleFuel;
using Cosmix.Infrastructure.Database;
using Cosmix.Infrastructure.Repositories.HR;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Query;

// Uses the existing standalone verifier pattern: real repository expressions over synthetic
// records, then offline SQL Server translation. No database or application host is started.
var checks = 0;
var failures = new List<string>();
void Check(bool value, string name)
{
    checks++;
    if (!value) { failures.Add(name); Console.WriteLine("FAIL " + name); }
}
var guard = new NoVehicleIntegrityConnections();
const string connection = "Server=vehicle-integrity.invalid;Database=Synthetic;Integrated Security=true;Encrypt=true";
using var db = new CosmixDbContext(new DbContextOptionsBuilder<CosmixDbContext>()
    .UseSqlServer(connection).ReplaceService<IAsyncQueryProvider, VehicleIntegrityQueries>()
    .AddInterceptors(guard).Options);
using var sqlDb = new CosmixDbContext(new DbContextOptionsBuilder<CosmixDbContext>()
    .UseSqlServer(connection).AddInterceptors(guard).Options);
var repository = new VehicleFuelRepository(db);
var employee = Guid.NewGuid();
var vehicle = Guid.NewGuid();
var date = new DateOnly(2026, 10, 19);
var now = new DateTimeOffset(2026, 10, 19, 7, 0, 0, TimeSpan.Zero);

EmployeeCommuteDistance Commute(DateOnly from, DateOnly? to, int revision = 1, Guid? owner = null,
    decimal distance = 30m, Guid? predecessor = null)
{
    var row = new EmployeeCommuteDistance(owner ?? employee, distance, "synthetic-route", from, to,
        "synthetic-user", new DateTimeOffset(2026, 10, 19, 0, 0, 0, TimeSpan.Zero), revision, predecessor);
    VehicleIntegrityData.Rows.Add(row);
    return row;
}
Task<EmployeeCommuteDistance?> Read(DateOnly? at = null, CancellationToken ct = default) =>
    repository.FindActiveCommuteDistanceAsync(employee, at ?? date, ct);
async Task Ambiguous()
{
    try { await Read(); }
    catch (AuthenticationFailure ex)
    {
        Check(ex.Code == "vehicle_commute_history_ambiguous" && ex.StatusCode == 409,
            "Ambiguous history produces the explicit operation conflict");
        return;
    }
    Check(false, "Ambiguous history must not select a distance");
}
async Task Scenario(string name, Func<Task> action)
{
    VehicleIntegrityData.Clear();
    VehicleIntegrityQueries.Captured.Clear();
    try { await action(); }
    catch (Exception ex) { Check(false, name + ": " + ex.GetBaseException().Message); }
    Check(VehicleIntegrityQueries.Captured.Count > 0, name + " executed repository query");
    foreach (var expression in VehicleIntegrityQueries.Captured)
    {
        try
        {
            var source = expression;
            if (expression is MethodCallExpression call && !typeof(IQueryable).IsAssignableFrom(expression.Type))
            {
                // Keep the predicate of Any/SingleOrDefault; dropping it would only test table translation.
                source = call.Arguments.Count == 2
                    ? Expression.Call(typeof(Queryable), nameof(Queryable.Where), call.Method.GetGenericArguments(),
                        call.Arguments[0], call.Arguments[1])
                    : call.Arguments[0];
            }
            var sql = ((IQueryable)sqlDb.EmployeeCommuteDistances).Provider.CreateQuery(source).ToQueryString();
            Check(sql.Contains("SELECT", StringComparison.OrdinalIgnoreCase) &&
                sql.Contains("WHERE", StringComparison.OrdinalIgnoreCase), name + " filtered offline SQL translation");
        }
        catch (Exception ex) { Check(false, name + " SQL translation: " + ex.GetBaseException().Message); }
    }
}

await Scenario("empty history", async () => Check(await Read() is null, "No baseline remains absent"));
await Scenario("single open history", async () =>
{
    var expected = Commute(date, null);
    Check((await Read())?.Id == expected.Id, "Inclusive start returns exact row");
    Check((await Read(date.AddYears(1)))?.Id == expected.Id, "Open end remains effective");
});
await Scenario("employee and date boundaries", async () =>
{
    Commute(date.AddDays(-10), date);
    Commute(date.AddDays(1), null, 2, predecessor: Guid.NewGuid());
    Commute(date, null, owner: Guid.NewGuid());
    Check(await Read() is null, "Expired, future and foreign rows are excluded");
});
await Scenario("adjacent intervals", async () =>
{
    var prior = Commute(date.AddDays(-10), date);
    var current = Commute(date, date.AddDays(10));
    Check((await Read(date.AddDays(-1)))?.Id == prior.Id, "Prior day selects prior baseline");
    Check((await Read())?.Id == current.Id, "Exclusive expiry permits adjacent baseline");
    Check(await Read(date.AddDays(10)) is null, "Finite end is exclusive");
});
await Scenario("overlapping initial revisions", async () =>
{
    Commute(date.AddDays(-2), null, distance: 20m);
    Commute(date.AddDays(-1), null, distance: 80m);
    await Ambiguous();
    VehicleIntegrityData.Rows.Reverse();
    await Ambiguous();
});
await Scenario("equal distances still ambiguous", async () =>
{
    Commute(date, null);
    Commute(date, null);
    await Ambiguous();
});
await Scenario("revision rank cannot resolve overlapping validity", async () =>
{
    var prior = Commute(date.AddDays(-10), null);
    Commute(date, null, 2, predecessor: prior.Id);
    await Ambiguous();
    Check(prior.EffectiveTo is null, "Read does not rewrite predecessor history");
});
await Scenario("finite overlap", async () =>
{
    Commute(date.AddDays(-10), date.AddDays(1));
    Commute(date.AddDays(-1), date.AddDays(10));
    await Ambiguous();
});
await Scenario("many overlaps bounded to two candidates", async () =>
{
    for (var i = 0; i < 25; i++) Commute(date.AddDays(-i), null);
    await Ambiguous();
    var expression = VehicleIntegrityQueries.Captured.Single();
    Check(expression is MethodCallExpression call && call.Method.Name == nameof(Queryable.Take) &&
        call.Arguments[1] is ConstantExpression { Value: 2 }, "Repository requests at most two effective rows");
});
await Scenario("cancellation", async () =>
{
    Commute(date, null);
    using var cancelled = new CancellationTokenSource();
    cancelled.Cancel();
    try { await Read(ct: cancelled.Token); Check(false, "Cancelled query must not return a baseline"); }
    catch (OperationCanceledException) { Check(true, "Cancellation propagates"); }
});

await Scenario("commute proposed intervals", async () =>
{
    Commute(date, date.AddDays(2));
    Check(await repository.HasCommutePeriodOverlapAsync(employee, date.AddDays(-1), null, default), "Open proposed interval includes future history");
    Check(await repository.HasCommutePeriodOverlapAsync(employee, date.AddDays(1), date.AddDays(3), default), "Finite overlap rejected");
    Check(await repository.HasCommutePeriodOverlapAsync(employee, date, date.AddDays(2), default), "Exact duplicate period rejected");
    Check(!await repository.HasCommutePeriodOverlapAsync(employee, date.AddDays(-1), date, default), "Adjacent earlier period allowed");
    Check(!await repository.HasCommutePeriodOverlapAsync(employee, date.AddDays(2), null, default), "Adjacent later open period allowed");
    Check(!await repository.HasCommutePeriodOverlapAsync(Guid.NewGuid(), date, null, default), "Foreign employee does not conflict");
});
await Scenario("commute existing open interval", async () =>
{
    Commute(date, null);
    Check(await repository.HasCommutePeriodOverlapAsync(employee, date.AddYears(1), null, default), "Open history blocks later overlap");
});

VehicleCustody Custody(DateTimeOffset start, DateTimeOffset? end = null, Guid? vehicleId = null, Guid? employeeId = null) =>
    VehicleIntegrityData.Add<VehicleCustody>(("VehicleId", vehicleId ?? vehicle), ("EmployeeId", employeeId ?? employee),
        ("IssuedAt", start), ("ReturnedAt", end), ("IssueOdometerKm", 100m));
VehicleTrip Trip(DateTimeOffset start, DateTimeOffset end, decimal fromKm, decimal toKm,
    ApprovalState state = ApprovalState.Pending, Guid? vehicleId = null, Guid? employeeId = null)
{
    var approval = VehicleIntegrityData.Add<ApprovalRequest>(("State", state));
    return VehicleIntegrityData.Add<VehicleTrip>(("VehicleId", vehicleId ?? vehicle), ("EmployeeId", employeeId ?? employee),
        ("StartedAt", start), ("EndedAt", end), ("OdometerStartKm", fromKm), ("OdometerEndKm", toKm),
        ("ApprovalRequestId", approval.Id), ("ApprovalRequest", approval));
}
VehicleServiceRecord Service(DateOnly day, decimal km, ApprovalState state = ApprovalState.Pending,
    Guid? vehicleId = null, Guid? employeeId = null)
{
    var approval = VehicleIntegrityData.Add<ApprovalRequest>(("State", state));
    return VehicleIntegrityData.Add<VehicleServiceRecord>(("VehicleId", vehicleId ?? vehicle), ("EmployeeId", employeeId ?? employee),
        ("ServiceDate", day), ("OdometerKm", km), ("ApprovalRequestId", approval.Id), ("ApprovalRequest", approval));
}
Task<bool> TripOverlap(Guid? exclude = null, decimal fromKm = 200m, decimal toKm = 300m) =>
    repository.HasTripOverlapAsync(vehicle, now, now.AddHours(1), fromKm, toKm, exclude, default);

await Scenario("custody returned history and issue boundary", async () =>
{
    Custody(now.AddDays(-2), now);
    Check(await repository.HasCustodyPeriodOverlapAsync(vehicle, Guid.NewGuid(), now.AddHours(-1), default), "Closed vehicle custody blocks backdated issue");
    Check(await repository.HasCustodyPeriodOverlapAsync(Guid.NewGuid(), employee, now.AddHours(-1), default), "Closed employee custody blocks different vehicle");
    Check(!await repository.HasCustodyPeriodOverlapAsync(vehicle, employee, now, default), "Touching return and new issue allowed");
    Check(!await repository.HasCustodyPeriodOverlapAsync(Guid.NewGuid(), Guid.NewGuid(), now.AddHours(-1), default), "Unrelated custody excluded");
});
await Scenario("custody future and open history", async () =>
{
    Custody(now.AddDays(2), now.AddDays(3));
    Check(await repository.HasCustodyPeriodOverlapAsync(vehicle, employee, now, default), "Proposed open issue overlaps future closed custody");
    Custody(now.AddDays(-3));
    Check(await repository.HasCustodyPeriodOverlapAsync(vehicle, employee, now.AddYears(1), default), "Existing open custody blocks future issue");
});
foreach (var state in Enum.GetValues<ApprovalState>())
{
    await Scenario("trip reservation state " + state, async () =>
    {
        Trip(now.AddMinutes(30), now.AddHours(2), 400m, 500m, state);
        Check(await TripOverlap() == (state is ApprovalState.Pending or ApprovalState.Approved), "Only Pending/Approved time overlap reserved");
    });
    await Scenario("trip odometer state " + state, async () =>
    {
        Trip(now.AddDays(-1), now.AddDays(-1).AddHours(1), 250m, 350m, state);
        Check(await TripOverlap() == (state is ApprovalState.Pending or ApprovalState.Approved), "Only Pending/Approved odometer overlap reserved");
    });
}
await Scenario("trip own identity and vehicle scope", async () =>
{
    var own = Trip(now, now.AddHours(1), 200m, 300m);
    Trip(now, now.AddHours(1), 200m, 300m, vehicleId: Guid.NewGuid());
    Check(!await TripOverlap(own.Id), "Own trip and different vehicle excluded");
    Trip(now, now.AddHours(1), 200m, 300m, employeeId: Guid.NewGuid());
    Check(await TripOverlap(own.Id), "Same vehicle conflict survives employee change");
});
await Scenario("trip touching endpoints and empty odometer ranges", async () =>
{
    Trip(now.AddHours(-1), now, 100m, 200m);
    Trip(now.AddHours(1), now.AddHours(2), 300m, 400m);
    Trip(now.AddDays(-1), now.AddDays(-1).AddHours(1), 250m, 250m);
    Check(!await TripOverlap(), "Adjacent time/odometer and empty existing odometer ranges allowed");
    Trip(now.AddDays(-2), now.AddDays(-2).AddHours(1), 150m, 350m);
    Check(!await TripOverlap(fromKm: 250m, toKm: 250m), "Empty proposed odometer range cannot overlap");
    Trip(now, now.AddHours(1), 250m, 250m);
    Check(await TripOverlap(fromKm: 250m, toKm: 250m), "Time overlap still blocks zero-distance record");
});
await Scenario("trip missing approval", async () =>
{
    var row = Trip(now, now.AddHours(1), 200m, 300m);
    typeof(VehicleTrip).GetProperty("ApprovalRequest")!.SetValue(row, null);
    Check(!await TripOverlap(), "Unsubmitted trip does not reserve an interval");
});
foreach (var state in Enum.GetValues<ApprovalState>())
{
    await Scenario("custody return trip state " + state, async () =>
    {
        var custody = Custody(now.AddDays(-1));
        Trip(now.AddHours(-1), now.AddHours(1), 100m, 200m, state);
        Check(await repository.HasCustodyReturnConflictAsync(custody.Id, now, 200m, default) ==
            (state is ApprovalState.Pending or ApprovalState.Approved), "Return time protects only Pending/Approved trips");
        Check(await repository.HasCustodyReturnConflictAsync(custody.Id, now.AddHours(1), 199m, default) ==
            (state is ApprovalState.Pending or ApprovalState.Approved), "Return odometer protects only Pending/Approved trips");
    });
    await Scenario("custody return service state " + state, async () =>
    {
        var custody = Custody(now.AddDays(-1));
        Service(date.AddDays(1), 200m, state);
        Check(await repository.HasCustodyReturnConflictAsync(custody.Id, now, 200m, default) ==
            (state is ApprovalState.Pending or ApprovalState.Approved), "Return date protects only Pending/Approved services");
    });
}
await Scenario("custody return exact trip boundaries", async () =>
{
    var custody = Custody(now.AddDays(-1));
    Trip(now.AddHours(-1), now, 100m, 200m);
    Check(!await repository.HasCustodyReturnConflictAsync(custody.Id, now, 200m, default), "Trip ending at return with equal odometer remains covered");
    Check(custody.ReturnedAt is null, "Conflict query does not mutate custody");
});
await Scenario("custody return trip scope", async () =>
{
    var custody = Custody(now.AddDays(-1), now.AddDays(1));
    Trip(now.AddDays(-3), now.AddDays(-2), 100m, 500m);
    Trip(now.AddDays(2), now.AddDays(3), 100m, 500m);
    Trip(now, now.AddHours(1), 100m, 500m, vehicleId: Guid.NewGuid());
    Trip(now, now.AddHours(1), 100m, 500m, employeeId: Guid.NewGuid());
    Check(!await repository.HasCustodyReturnConflictAsync(custody.Id, now, 200m, default), "Foreign and original-period-external trips excluded");
});
await Scenario("custody service local day and odometer", async () =>
{
    var custody = Custody(now.AddDays(-1));
    Service(date, 200m);
    var midnight = new DateTimeOffset(2026, 10, 19, 0, 0, 0, TimeSpan.FromHours(5));
    Check(await repository.HasCustodyReturnConflictAsync(custody.Id, midnight, 200m, default), "Return at Pakistan midnight excludes that service day");
    Check(!await repository.HasCustodyReturnConflictAsync(custody.Id, midnight.AddMinutes(1), 200m, default), "Same local day overlap remains possible after midnight");
    Check(await repository.HasCustodyReturnConflictAsync(custody.Id, now, 199m, default), "Service odometer above return conflicts");
    Check(!await repository.HasCustodyReturnConflictAsync(custody.Id, now, 200m, default), "Equal service odometer remains valid");
});
await Scenario("custody service original coverage and identity", async () =>
{
    var originalEnd = new DateTimeOffset(2026, 10, 20, 0, 0, 0, TimeSpan.FromHours(5));
    var custody = Custody(now.AddDays(-1), originalEnd);
    Service(date.AddDays(-2), 500m);
    Service(date.AddDays(1), 500m);
    Service(date, 500m, vehicleId: Guid.NewGuid());
    Service(date, 500m, employeeId: Guid.NewGuid());
    Check(!await repository.HasCustodyReturnConflictAsync(custody.Id, now, 200m, default), "Out-of-period and foreign services excluded");
});
await Scenario("missing custody fails closed", async () =>
{
    try
    {
        await repository.HasCustodyReturnConflictAsync(Guid.NewGuid(), now, 200m, default);
        Check(false, "Missing custody must not permit return");
    }
    catch (AuthenticationFailure ex) { Check(ex.Code == "custody_not_found" && ex.StatusCode == 404, "Missing custody remains explicit"); }
});
Check(guard.Attempts == 0, "Zero SQL connection attempts");
Console.WriteLine($"Vehicle integrity repository: {checks - failures.Count}/{checks} checks passed; synthetic execution/offline SQL translation; SQL attempts={guard.Attempts}.");
return failures.Count == 0 ? 0 : 1;

static class VehicleIntegrityData
{
    public static List<EmployeeCommuteDistance> Rows { get; } = [];
    private static readonly Dictionary<Type, IList> OtherRows = [];
    public static void Clear() { Rows.Clear(); OtherRows.Clear(); }
    public static T Add<T>(params (string Name, object? Value)[] fields) where T : class
    {
        var row = (T)Activator.CreateInstance(typeof(T), true)!;
        typeof(T).GetProperty("Id")!.SetValue(row, Guid.NewGuid());
        foreach (var (name, value) in fields) typeof(T).GetProperty(name)!.SetValue(row, value);
        if (!OtherRows.TryGetValue(typeof(T), out var rows)) OtherRows[typeof(T)] = rows = new List<T>();
        rows.Add(row);
        return row;
    }
    public static IQueryable Query(Type type)
    {
        if (type == typeof(EmployeeCommuteDistance)) return Rows.AsQueryable();
        if (!OtherRows.TryGetValue(type, out var rows)) rows = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(type))!;
        return (IQueryable)typeof(Queryable).GetMethods().Single(m => m.Name == nameof(Queryable.AsQueryable) && m.IsGenericMethod)
            .MakeGenericMethod(type).Invoke(null, [rows])!;
    }
}
public sealed class VehicleIntegrityQueries : IAsyncQueryProvider
{
    public static List<Expression> Captured { get; } = [];
    public IQueryable CreateQuery(Expression expression) => (IQueryable)Activator.CreateInstance(
        typeof(VehicleIntegrityQuery<>).MakeGenericType(expression.Type.GetGenericArguments()[0]), this, expression)!;
    public IQueryable<T> CreateQuery<T>(Expression expression) => new VehicleIntegrityQuery<T>(this, expression);
    public object? Execute(Expression expression) => Expression.Lambda(new Roots().Visit(expression)!).Compile().DynamicInvoke();
    public T Execute<T>(Expression expression) => (T)Execute(expression)!;
    public T ExecuteAsync<T>(Expression expression, CancellationToken ct = default)
    {
        Captured.Add(expression);
        ct.ThrowIfCancellationRequested();
        var element = typeof(T).GetGenericArguments()[0];
        return (T)typeof(Task).GetMethod(nameof(Task.FromResult))!.MakeGenericMethod(element)
            .Invoke(null, [Execute(expression)])!;
    }
    private sealed class Roots : ExpressionVisitor
    {
        protected override Expression VisitExtension(Expression node) => node is EntityQueryRootExpression root
            ? VehicleIntegrityData.Query(root.EntityType.ClrType).Expression : base.VisitExtension(node);
        protected override Expression VisitMethodCall(MethodCallExpression node) =>
            node.Method.DeclaringType == typeof(EntityFrameworkQueryableExtensions) && node.Method.Name == "AsNoTracking"
                ? Visit(node.Arguments[0]) : base.VisitMethodCall(node);
    }
}
public sealed class VehicleIntegrityQuery<T>(VehicleIntegrityQueries provider, Expression expression)
    : IOrderedQueryable<T>, IAsyncEnumerable<T>
{
    public Type ElementType => typeof(T);
    public Expression Expression => expression;
    public IQueryProvider Provider => provider;
    public IEnumerator<T> GetEnumerator() => provider.Execute<IEnumerable<T>>(expression).GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    public async IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken ct = default)
    {
        VehicleIntegrityQueries.Captured.Add(expression);
        ct.ThrowIfCancellationRequested();
        await Task.CompletedTask;
        foreach (var row in this) { ct.ThrowIfCancellationRequested(); yield return row; }
    }
}
sealed class NoVehicleIntegrityConnections : DbConnectionInterceptor
{
    public int Attempts;
    public override InterceptionResult ConnectionOpening(DbConnection c, ConnectionEventData e, InterceptionResult r)
    { Attempts++; throw new Exception("SQL connections forbidden"); }
    public override ValueTask<InterceptionResult> ConnectionOpeningAsync(DbConnection c, ConnectionEventData e,
        InterceptionResult r, CancellationToken ct = default)
    { Attempts++; throw new Exception("SQL connections forbidden"); }
}
