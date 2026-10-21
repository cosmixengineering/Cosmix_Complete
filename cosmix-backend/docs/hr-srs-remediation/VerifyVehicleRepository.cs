using System.Collections;
using System.Data.Common;
using System.Linq.Expressions;
using System.Reflection;
using Cosmix.Application.Authentication;
using Cosmix.Application.HR.VehicleFuel;
using Cosmix.Domain.Common.Approvals;
using Cosmix.Domain.HR.Policies;
using Cosmix.Domain.HR.VehicleFuel;
using Cosmix.Infrastructure.Database;
using Cosmix.Infrastructure.Repositories.HR;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Query;

// Executes the actual repository expressions against synthetic records. A second SQL Server
// provider translates those expressions offline. Neither is an actual SQL execution test.
var checks = 0; var failures = new List<string>();
void Check(bool value, string name) { checks++; if (!value) { failures.Add(name); Console.WriteLine("FAIL " + name); } }
var guard = new NoVehicleConnections();
using var db = new CosmixDbContext(new DbContextOptionsBuilder<CosmixDbContext>()
    .UseSqlServer("Server=vehicle-tests.invalid;Database=Synthetic;Integrated Security=true;Encrypt=true")
    .ReplaceService<IAsyncQueryProvider, VehicleMemoryQueries>().AddInterceptors(guard).Options);
using var sqlDb = new CosmixDbContext(new DbContextOptionsBuilder<CosmixDbContext>()
    .UseSqlServer("Server=vehicle-tests.invalid;Database=Synthetic;Integrated Security=true;Encrypt=true").AddInterceptors(guard).Options);
var repo = new VehicleFuelRepository(db);
var date = new DateOnly(2026,10,9); var now = new DateTimeOffset(2026,10,9,7,0,0,TimeSpan.Zero);
var vehicle = Guid.NewGuid(); var employee = Guid.NewGuid();
async Task<T> Call<T>(string name, params object?[] args)
{
    var method = typeof(VehicleFuelRepository).GetMethod(name) ?? throw new Exception("Repository method missing: " + name);
    // Baseline signatures lack the business-date argument; retain a runnable red test against that binary.
    if (method.GetParameters().Length != args.Length) args = args.Where(x => x is not DateOnly).ToArray();
    Task task;
    try { task = (Task)method.Invoke(repo,args)!; } catch (TargetInvocationException ex) { throw ex.InnerException!; }
    await task; return (T)task.GetType().GetProperty("Result")!.GetValue(task)!;
}
async Task Scenario(string name, Func<Task> action)
{
    VehicleData.Clear(); VehicleMemoryQueries.Captured.Clear();
    try { await action(); }
    catch (Exception ex) { Check(false,name + ": " + ex.GetBaseException().Message); }
    foreach (var expression in VehicleMemoryQueries.Captured.ToArray())
    {
        try
        {
            // Scalar async operators are checked through their complete filtered source expression.
            var source = expression is MethodCallExpression call && !typeof(IQueryable).IsAssignableFrom(expression.Type)
                ? call.Arguments[0] : expression;
            var sql = ((IQueryable)sqlDb.Vehicles).Provider.CreateQuery(source).ToQueryString();
            Check(sql.Contains("SELECT",StringComparison.OrdinalIgnoreCase),name + " offline SQL translation");
        }
        catch (Exception ex) { Check(false,name + " offline SQL translation: " + ex.GetBaseException().Message); }
    }
}
async Task Reject(Func<Task> action,string code)
{
    try { await action(); } catch (AuthenticationFailure ex) when (ex.Code == code) { Check(true,code); return; }
    Check(false,"Expected " + code);
}
PolicyVersion Policy(DateOnly from, DateOnly? to, int version = 1, bool active = true)
{
    var policy = VehicleData.Add<Policy>(("Category",PolicyCategory.VehicleFuel),("IsActive",active));
    return VehicleData.Add<PolicyVersion>(("Policy",policy),("PolicyId",policy.Id),("EffectiveFrom",from),("EffectiveTo",to),
        ("VersionNumber",version),("Authority",PolicyAuthority.DirectHr),("RulesJson","{}"));
}
VehicleMileageEntry Mileage(decimal km, DateOnly day, ApprovalState state = ApprovalState.Approved, bool decision = true, Guid? vehicleId = null)
{
    var trip = VehicleData.Add<VehicleTrip>(("VehicleId",vehicleId ?? vehicle),("EmployeeId",employee));
    var approval = VehicleData.Add<ApprovalRequest>(("SubjectId",trip.Id),("State",state),("RequestType",VehicleFuelService.TripRequestType));
    typeof(VehicleTrip).GetProperty("ApprovalRequestId")!.SetValue(trip,approval.Id);
    if (decision) VehicleData.Add<ApprovalDecision>(("ApprovalRequestId",approval.Id),("Decision",state));
    return VehicleData.Add<VehicleMileageEntry>(("VehicleId",vehicleId ?? vehicle),("EmployeeId",employee),("VehicleTripId",trip.Id),
        ("VehicleTrip",trip),("ApprovalRequestId",approval.Id),("ApprovalRequest",approval),("BusinessDate",day),("OutstationKm",km));
}
await Scenario("effective policy date",async () => {
    var expected = Policy(new(2026,10,1),new(2026,11,1)); Policy(new(2026,11,1),null,99);
    Check((await Call<PolicyVersion?>("GetActiveVehicleFuelPolicyVersionAsync",date,CancellationToken.None))?.Id == expected.Id,
        "Future higher version cannot price October trip");
});
await Scenario("exclusive policy expiry",async () => {
    Policy(new(2026,9,1),date,99);
    Check(await Call<PolicyVersion?>("GetActiveVehicleFuelPolicyVersionAsync",date,CancellationToken.None) is null,"Expired policy is unavailable at exclusive end");
});
await Scenario("unapproved publication",async () => {
    var row = Policy(new(2026,10,1),null);
    var approval = VehicleData.Add<ApprovalRequest>(("SubjectId",row.PolicyId),("State",ApprovalState.Pending));
    typeof(PolicyVersion).GetProperty("Authority")!.SetValue(row,PolicyAuthority.ApprovedWorkflow);
    typeof(PolicyVersion).GetProperty("ApprovalRequestId")!.SetValue(row,approval.Id);
    typeof(PolicyVersion).GetProperty("ApprovalRequest")!.SetValue(row,approval);
    Check(await Call<PolicyVersion?>("GetActiveVehicleFuelPolicyVersionAsync",date,CancellationToken.None) is null,"Pending approval is not authority");
});
await Scenario("approved workflow publication",async () => {
    var row = Policy(new(2026,10,1),null);
    var approval = VehicleData.Add<ApprovalRequest>(("SubjectId",row.PolicyId),("State",ApprovalState.Approved));
    typeof(PolicyVersion).GetProperty("Authority")!.SetValue(row,PolicyAuthority.ApprovedWorkflow);
    typeof(PolicyVersion).GetProperty("ApprovalRequestId")!.SetValue(row,approval.Id);
    typeof(PolicyVersion).GetProperty("ApprovalRequest")!.SetValue(row,approval);
    Check(await Call<PolicyVersion?>("GetActiveVehicleFuelPolicyVersionAsync",date,CancellationToken.None) is null,"Approval state alone is not a retained decision");
    VehicleData.Add<ApprovalDecision>(("ApprovalRequestId",approval.Id),("Decision",ApprovalState.Approved));
    Check((await Call<PolicyVersion?>("GetActiveVehicleFuelPolicyVersionAsync",date,CancellationToken.None))?.Id == row.Id,"Approved workflow decision is eligible");
});
await Scenario("ambiguous policy",async () => {
    Policy(new(2026,10,1),null); Policy(new(2026,10,1),null,2);
    await Reject(() => Call<PolicyVersion?>("GetActiveVehicleFuelPolicyVersionAsync",date,CancellationToken.None),"vehicle_fuel_policy_ambiguous");
});
await Scenario("retained inactive policy",async () => {
    var row = Policy(new(2026,10,1),null,active:false);
    Check((await Call<PolicyVersion?>("FindVehicleFuelPolicyVersionAsync",row.Id,CancellationToken.None))?.Id == row.Id,
        "Pinned historical authority does not silently reprice after catalog deactivation");
});
await Scenario("as-of authoritative mileage",async () => {
    Mileage(120.25m,date.AddDays(-1)); Mileage(59.75m,date); Mileage(900,date.AddDays(1));
    Mileage(500,date,ApprovalState.Pending); Mileage(700,date,decision:false); Mileage(800,date,vehicleId:Guid.NewGuid());
    var foreign = Mileage(600,date); typeof(VehicleMileageEntry).GetProperty("EmployeeId")!.SetValue(foreign,Guid.NewGuid());
    Check(await Call<decimal>("GetAccumulatedOutstationMileageAsync",vehicle,date,CancellationToken.None) == 180,
        "120.25 + 59.75 only; future/pending/missing decision/other vehicle/wrong employee excluded");
});
await Scenario("ambiguous trip mileage",async () => {
    var original = Mileage(100,date);
    VehicleData.Add<VehicleMileageEntry>(("VehicleId",vehicle),("EmployeeId",employee),("VehicleTripId",original.VehicleTripId),
        ("VehicleTrip",original.VehicleTrip),("ApprovalRequestId",original.ApprovalRequestId),("ApprovalRequest",original.ApprovalRequest));
    await Reject(() => Call<VehicleMileageEntry?>("FindApprovedMileageForTripAsync",original.VehicleTripId,CancellationToken.None),"approved_trip_mileage_ambiguous");
});
await Scenario("custody exact interval",async () => {
    var prior = VehicleData.Add<VehicleCustody>(("VehicleId",vehicle),("IssuedAt",now.AddDays(-1)),("ReturnedAt",now));
    var current = VehicleData.Add<VehicleCustody>(("VehicleId",vehicle),("IssuedAt",now),("ReturnedAt",now.AddHours(1)));
    VehicleData.Add<VehicleCustody>(("VehicleId",vehicle),("IssuedAt",now.AddHours(1)));
    var result = await Call<IReadOnlyList<VehicleCustody>>("FindCustodiesOverlappingAsync",vehicle,now,now.AddHours(1),CancellationToken.None);
    Check(result.Count == 1 && result[0].Id == current.Id && result[0].Id != prior.Id,"Adjacent custody boundaries do not overlap");
});
await Scenario("retained mileage source membership",async () => {
    var expected = Mileage(180,date); Mileage(900,date.AddDays(1)); Mileage(700,date,decision:false);
    Mileage(500,date,ApprovalState.Pending); Mileage(300,date,vehicleId:Guid.NewGuid());
    var result = await Call<IReadOnlyList<VehicleMileageEntry>>("ListApprovedMileageAsync",vehicle,date,CancellationToken.None);
    Check(result.Count == 1 && result[0].Id == expected.Id,"Cycle fingerprint excludes future/pending/unapproved/foreign mileage");
});
await Scenario("latest cycle requires exact retained service decision",async () => {
    VehicleServiceRecord Service(ApprovalState state,DateTimeOffset at,bool decision=true,Guid? owner=null,VehicleServiceKind kind=VehicleServiceKind.Tuning)
    {
        var row = VehicleData.Add<VehicleServiceRecord>(("VehicleId",owner ?? vehicle),("ServiceKind",kind));
        var approval = VehicleData.Add<ApprovalRequest>(("SubjectId",row.Id),("State",state),("ClosedAt",at),
            ("RequestType",VehicleFuelService.ServiceRequestType));
        typeof(VehicleServiceRecord).GetProperty("ApprovalRequest")!.SetValue(row,approval);
        typeof(VehicleServiceRecord).GetProperty("ApprovalRequestId")!.SetValue(row,approval.Id);
        if (decision) VehicleData.Add<ApprovalDecision>(("ApprovalRequestId",approval.Id),("Decision",state));
        return row;
    }
    Service(ApprovalState.Approved,now.AddHours(-1)); var expected = Service(ApprovalState.Approved,now);
    Service(ApprovalState.Pending,now.AddHours(1)); Service(ApprovalState.Approved,now.AddHours(2),decision:false);
    Service(ApprovalState.Approved,now.AddHours(3),owner:Guid.NewGuid());
    Service(ApprovalState.Approved,now.AddHours(4),kind:VehicleServiceKind.EngineOverhaul);
    var foreign = Service(ApprovalState.Approved,now.AddHours(5));
    typeof(ApprovalRequest).GetProperty("SubjectId")!.SetValue(foreign.ApprovalRequest,Guid.NewGuid());
    var result = await Call<VehicleServiceRecord?>("FindLatestApprovedServiceAsync",vehicle,VehicleServiceKind.Tuning,CancellationToken.None);
    Check(result?.Id == expected.Id,"Cycle continuation uses correct vehicle/kind and actual decision, without trusting pending or foreign approvals");
    Service(ApprovalState.Approved,now);
    await Reject(() => Call<VehicleServiceRecord?>("FindLatestApprovedServiceAsync",vehicle,VehicleServiceKind.Tuning,CancellationToken.None),
        "vehicle_service_cycle_order_ambiguous");
});
await Scenario("service evidence reservation",async () => {
    var approval = VehicleData.Add<ApprovalRequest>(("State",ApprovalState.Pending));
    VehicleData.Add<VehicleServiceRecord>(("VehicleId",vehicle),("EvidencePrivateReference","service-proof"),("ApprovalRequest",approval));
    Check(await Call<bool>("HasServiceEvidenceAsync",vehicle,"service-proof",CancellationToken.None),"Pending evidence is reserved");
    typeof(ApprovalRequest).GetProperty("State")!.SetValue(approval,ApprovalState.Approved);
    Check(await Call<bool>("HasServiceEvidenceAsync",vehicle,"service-proof",CancellationToken.None),"Approved evidence cannot be reused");
    Check(!await Call<bool>("HasServiceEvidenceAsync",Guid.NewGuid(),"service-proof",CancellationToken.None),"Other vehicle is not the same reservation");
    typeof(ApprovalRequest).GetProperty("State")!.SetValue(approval,ApprovalState.Rejected);
    Check(!await Call<bool>("HasServiceEvidenceAsync",vehicle,"service-proof",CancellationToken.None),"Rejected proposal has not consumed service eligibility");
});
await Scenario("adjustment history empty for unrelated subject or request type",async () => {
    VehicleData.Add<ApprovalRequest>(("SubjectId",Guid.NewGuid()),("RequestType","vehicle.monthly-adjustment"),("Stage",1));
    VehicleData.Add<ApprovalRequest>(("SubjectId",vehicle),("RequestType","vehicle.incident.amendment"),("Stage",1));
    var rows = await Call<IReadOnlyList<ApprovalRequest>>("ListVehicleAdjustmentRequestsAsync",vehicle,"vehicle.monthly-adjustment",CancellationToken.None);
    Check(rows.Count == 0,"No monthly records means explicit empty history, not unrelated incident evidence");
});
await Scenario("adjustment history retains every revision and exact request type",async () => {
    var second = VehicleData.Add<ApprovalRequest>(("SubjectId",vehicle),("RequestType","vehicle.monthly-adjustment"),("Stage",2),("State",ApprovalState.Pending));
    var first = VehicleData.Add<ApprovalRequest>(("SubjectId",vehicle),("RequestType","vehicle.monthly-adjustment"),("Stage",1),("State",ApprovalState.Returned));
    var rows = await Call<IReadOnlyList<ApprovalRequest>>("ListVehicleAdjustmentRequestsAsync",vehicle,"vehicle.monthly-adjustment",CancellationToken.None);
    Check(rows.Select(r => r.Id).SequenceEqual(new[] { first.Id, second.Id }),"Returned and pending revisions remain ordered history");
    Check((await Call<ApprovalRequest?>("FindVehicleAdjustmentRequestAsync",second.Id,"vehicle.monthly-adjustment",CancellationToken.None))?.Id == second.Id,"Exact revision found");
    Check(await Call<ApprovalRequest?>("FindVehicleAdjustmentRequestAsync",second.Id,"vehicle.incident.amendment",CancellationToken.None) is null,"ID cannot cross request types");
});
await Scenario("adjustment decision must be absent or unique for exact request",async () => {
    var request = Guid.NewGuid();
    VehicleData.Add<ApprovalDecision>(("ApprovalRequestId",Guid.NewGuid()),("Decision",ApprovalState.Approved));
    Check(await Call<ApprovalDecision?>("FindVehicleAdjustmentDecisionAsync",request,CancellationToken.None) is null,"No foreign decision substituted");
    var expected = VehicleData.Add<ApprovalDecision>(("ApprovalRequestId",request),("Decision",ApprovalState.Returned));
    Check((await Call<ApprovalDecision?>("FindVehicleAdjustmentDecisionAsync",request,CancellationToken.None))?.Id == expected.Id,"Retained return decision found");
    VehicleData.Add<ApprovalDecision>(("ApprovalRequestId",request),("Decision",ApprovalState.Approved));
    await Reject(() => Call<ApprovalDecision?>("FindVehicleAdjustmentDecisionAsync",request,CancellationToken.None),"vehicle_adjustment_decision_ambiguous");
});
await Scenario("monthly commute evidence is resolved by exact historical ID",async () => {
    var first = VehicleData.Add<EmployeeCommuteDistance>(("EmployeeId",employee),("Revision",1),("UsualHomeOfficeKm",30m));
    VehicleData.Add<EmployeeCommuteDistance>(("EmployeeId",employee),("Revision",2),("UsualHomeOfficeKm",500m));
    Check((await Call<EmployeeCommuteDistance?>("FindCommuteDistanceByIdAsync",first.Id,CancellationToken.None))?.UsualHomeOfficeKm == 30m,"Latest baseline cannot replace retained commute evidence");
    Check(await Call<EmployeeCommuteDistance?>("FindCommuteDistanceByIdAsync",Guid.NewGuid(),CancellationToken.None) is null,"Missing retained evidence is not a default");
});
await Scenario("explicit successor resolves old open period without mutation or resurrection",async () => {
    var original=Policy(new(2026,1,1),null);
    var next=VehicleData.Add<PolicyVersion>(("Policy",original.Policy),("PolicyId",original.PolicyId),("VersionNumber",2),
        ("EffectiveFrom",new DateOnly(2026,10,1)),("EffectiveTo",new DateOnly(2026,11,1)),("Authority",PolicyAuthority.DirectHr),
        ("RulesJson",System.Text.Json.JsonSerializer.Serialize(new { SchemaVersion=3,Succession=new { PreviousPolicyVersionId=original.Id,AuthorityReference="successor-evidence" } })));
    Check((await Call<PolicyVersion?>("GetActiveVehicleFuelPolicyVersionAsync",new DateOnly(2026,9,30),CancellationToken.None))?.Id==original.Id,"Pre-boundary selection retains old version");
    Check((await Call<PolicyVersion?>("GetActiveVehicleFuelPolicyVersionAsync",date,CancellationToken.None))?.Id==next.Id,"Successor resolves authorized overlap");
    Check(await Call<PolicyVersion?>("GetActiveVehicleFuelPolicyVersionAsync",new DateOnly(2026,11,1),CancellationToken.None) is null,"No predecessor resurrection");
    Check(original.EffectiveTo is null,"Stored predecessor remains immutable");
    Check(!await Call<bool>("HasPolicyPeriodOverlapAsync",new DateOnly(2026,11,1),new DateOnly(2026,12,1),CancellationToken.None),"Finite successor leaves a real future gap");
});
await Scenario("policy overlap reader retains active global history before resolution",async () => {
    var past=Policy(new(2026,1,1),new(2026,2,1));var future=Policy(new(2027,1,1),null);
    Policy(new(2026,1,1),null,active:false);
    var rows=await Call<IReadOnlyList<PolicyVersion>>("ListPolicyPeriodVersionsAsync",CancellationToken.None);
    Check(rows.Count==2&&rows.Any(v=>v.Id==past.Id)&&rows.Any(v=>v.Id==future.Id),"Global active-definition history includes past and future for successor validation");
});
Check(guard.Attempts == 0,"zero SQL connection attempts");
Console.WriteLine($"Vehicle repository: {checks-failures.Count}/{checks} checks passed; synthetic query execution/offline SQL translation, SQL attempts={guard.Attempts}.");
return failures.Count == 0 ? 0 : 1;

static class VehicleData
{
    private static readonly Dictionary<Type,IList> Data = [];
    public static T Add<T>(params (string Name,object? Value)[] fields) where T : class
    {
        var item = (T)Activator.CreateInstance(typeof(T),true)!;
        typeof(T).GetProperty("Id")!.SetValue(item,Guid.NewGuid());
        foreach (var (name,value) in fields) typeof(T).GetProperty(name)!.SetValue(item,value);
        if (!Data.TryGetValue(typeof(T),out var rows)) Data[typeof(T)] = rows = new List<T>();
        rows.Add(item); return item;
    }
    public static IQueryable Rows(Type type)
    {
        if (!Data.TryGetValue(type,out var rows)) rows = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(type))!;
        return (IQueryable)typeof(Queryable).GetMethods().Single(m => m.Name == "AsQueryable" && m.IsGenericMethod).MakeGenericMethod(type).Invoke(null,[rows])!;
    }
    public static void Clear() => Data.Clear();
}
public sealed class VehicleMemoryQueries : IAsyncQueryProvider
{
    public static List<Expression> Captured { get; } = [];
    public IQueryable CreateQuery(Expression expression) => (IQueryable)Activator.CreateInstance(typeof(VehicleMemoryQuery<>).MakeGenericType(expression.Type.GetGenericArguments()[0]),this,expression)!;
    public IQueryable<T> CreateQuery<T>(Expression expression) => new VehicleMemoryQuery<T>(this,expression);
    public object? Execute(Expression expression) => Expression.Lambda(new Roots().Visit(expression)!).Compile().DynamicInvoke();
    public T Execute<T>(Expression expression) => (T)Execute(expression)!;
    public T ExecuteAsync<T>(Expression expression,CancellationToken ct = default)
    {
        Captured.Add(expression); var element = typeof(T).GetGenericArguments()[0];
        if (typeof(T).GetGenericTypeDefinition() == typeof(Task<>))
            return (T)typeof(Task).GetMethod(nameof(Task.FromResult))!.MakeGenericMethod(element).Invoke(null,[Execute(expression)])!;
        return (T)Activator.CreateInstance(typeof(VehicleMemoryQuery<>).MakeGenericType(element),this,expression)!;
    }
    private sealed class Roots : ExpressionVisitor
    {
        protected override Expression VisitExtension(Expression node) => node is EntityQueryRootExpression root ? VehicleData.Rows(root.EntityType.ClrType).Expression : base.VisitExtension(node);
        protected override Expression VisitMethodCall(MethodCallExpression node) => node.Method.DeclaringType == typeof(EntityFrameworkQueryableExtensions)
            && node.Method.Name is "AsNoTracking" or "Include" or "ThenInclude" ? Visit(node.Arguments[0]) : base.VisitMethodCall(node);
    }
}
public sealed class VehicleMemoryQuery<T>(VehicleMemoryQueries provider,Expression expression) : IOrderedQueryable<T>,IAsyncEnumerable<T>
{
    public Type ElementType => typeof(T); public Expression Expression => expression; public IQueryProvider Provider => provider;
    public IEnumerator<T> GetEnumerator() => provider.Execute<IEnumerable<T>>(expression).GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    public async IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken ct = default)
    { VehicleMemoryQueries.Captured.Add(expression); await Task.CompletedTask; foreach(var row in this) { ct.ThrowIfCancellationRequested(); yield return row; } }
}
sealed class NoVehicleConnections : DbConnectionInterceptor
{
    public int Attempts;
    public override InterceptionResult ConnectionOpening(DbConnection c,ConnectionEventData e,InterceptionResult r)
    { Attempts++; throw new Exception("SQL connections forbidden"); }
    public override ValueTask<InterceptionResult> ConnectionOpeningAsync(DbConnection c,ConnectionEventData e,InterceptionResult r,CancellationToken ct = default)
    { Attempts++; throw new Exception("SQL connections forbidden"); }
}
