using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using Cosmix.Application.Attendance.Common;
using Cosmix.Application.Attendance.Shifts;
using Cosmix.Application.Authentication;
using Cosmix.Application.Authentication.Access;
using Cosmix.Application.Common.Files;
using Cosmix.Application.HR.VehicleFuel;
using Cosmix.Domain.Authentication;
using Cosmix.Domain.Common.Approvals;
using Cosmix.Domain.Common.Audit;
using Cosmix.Domain.Common.Idempotency;
using Cosmix.Domain.HR.Policies;
using Cosmix.Domain.HR.VehicleFuel;

var failures = new List<string>(); var passed = 0;
async Task Test(string name, Func<Task> action)
{
    try { await action(); passed++; Console.WriteLine("PASS " + name); }
    catch (Exception ex) { failures.Add(name + ": " + ex.GetBaseException().Message); Console.WriteLine("FAIL " + failures[^1]); }
}
void Check(bool value, string text) { if (!value) throw new Exception(text); }

PolicyVersion? Select(IReadOnlyList<PolicyVersion> rows, DateOnly date) {
    var type = typeof(VehicleFuelService).Assembly.GetType("Cosmix.Application.HR.VehicleFuel.VehiclePolicySuccession")
        ?? throw new Exception("Missing immutable succession resolver");
    return (PolicyVersion?)type.GetMethod("SelectActive")!.Invoke(null, new object[] { rows, date });
}
async Task<(SuccessionFixture F, PolicyVersion Old)> Seed() {
    var f = new SuccessionFixture(); await f.Create();
    var old = new PolicyVersion(f.Policy.Id, 1, new(2026,1,1), null, "legacy", f.Rules(new(2026,1,1), new(2027,1,1)).ToJsonString(),
        null, null, "prior-publisher", f.Clock.Now.AddMonths(-2), PolicyAuthority.DirectHr);
    f.Versions.Add(old); return (f,old);
}
JsonObject Successor(SuccessionFixture f, Guid prior) {
    var r = f.Rules(); r["SchemaVersion"] = 3; r["Adjustments"] = null;
    r["Succession"] = JsonSerializer.SerializeToNode(new { PreviousPolicyVersionId = prior, AuthorityReference = "approved-successor-evidence" }); return r;
}
await Test("explicit successor publication preserves open predecessor and resolves dated history without resurrection", async () => {
    var (f, old) = await Seed(); var before = JsonSerializer.Serialize(old);
    var request = await f.Prepare(rules: Successor(f, old.Id)); await f.Publish(request); await f.Publish(request);
    Check(f.Versions.Count == 2 && JsonSerializer.Serialize(old) == before && old.EffectiveTo is null, "No predecessor mutation or retry duplication");
    Check(Select(f.Versions,new(2026,10,31))!.Id == old.Id && Select(f.Versions,new(2026,11,1))!.Id == f.Versions[1].Id,
        "Successor starts exactly at approved date");
    Check(Select(f.Versions,new(2026,12,1)) is null, "Expired successor never resurrects open predecessor");
});
await Test("successor rejects foreign predecessor and another active definition", async () => {
    var (f, old) = await Seed(); f.AddExistingVersion(new(2026,10,1),new(2027,1,1));
    var validation = await f.Validate(Successor(f, old.Id));
    Check(!validation["IsValid"]!.GetValue<bool>(), "Conflicting definition cannot be superseded implicitly");
});
await Test("successor rejects duplicate branch and stale preview", async () => {
    var (f, old) = await Seed(); var first = await f.Prepare(rules: Successor(f,old.Id)); await f.Publish(first);
    var second = await f.Call("PreviewPolicyAsync",f.Policy.Id,f.PreviewRequest(rules: Successor(f,old.Id)));
    Check(!second["CanPublish"]!.GetValue<bool>(), "One explicit successor only");
});
await Test("successor schema requires exact primitive predecessor and authority", async () => {
    var (f, old) = await Seed(); var rules = Successor(f,old.Id);
    Check((await f.Validate(rules))["IsValid"]!.GetValue<bool>(), "Configured schema three accepted");
    rules["Succession"]!["PreviousPolicyVersionId"] = Guid.Empty;
    Check(!(await f.Validate(rules))["IsValid"]!.GetValue<bool>(), "Empty predecessor denied");
});
Console.WriteLine($"Vehicle succession: {passed}/{passed+failures.Count} scenarios; synthetic IO only.");
if(failures.Count > 0) Environment.ExitCode=1;

public sealed class SuccessionClock : TimeProvider
{
    public DateTimeOffset Now = new(2026, 10, 19, 7, 0, 0, TimeSpan.Zero);
    public override DateTimeOffset GetUtcNow() => Now;
}
public class SuccessionProxy : DispatchProxy
{
    public Func<MethodInfo, object?[], object?> Handler = null!;
    protected override object? Invoke(MethodInfo? method, object?[]? args) => Handler(method!, args ?? []);
    public static T Make<T>(Func<MethodInfo, object?[], object?> handler) where T : class
    { var value = Create<T, SuccessionProxy>(); ((SuccessionProxy)(object)value).Handler = handler; return value; }
}
public sealed class SuccessionFixture
{
    public const string Permission = "attendance.vehicle.policy.manage";
    public string PermissionCode => Permission;
    public Guid Employee = Guid.NewGuid(), SystemAssignment = Guid.NewGuid();
    public AuthenticatedAccount Actor = new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 1, SessionClient.Web, true);
    public bool CurrentSession = true, SystemRole = true, EmployeeExists = true, RoleActive = true, BlockFileReads;
    public int ExpectedVersions, Stamp;
    public DateOnly From = new(2026, 11, 1), To = new(2026, 12, 1);
    public SuccessionClock Clock = new();
    public AuthRequestContext Context = new("127.0.0.1", "synthetic-vehicle-publication");
    public List<Policy> Policies = [];
    public List<PolicyVersion> Versions = [];
    public List<PermissionGrant> Grants = [];
    public List<AuditEvent> Audits = [];
    public List<IdempotencyReceipt> Receipts = [];
    public Dictionary<string, (string Purpose, byte[] Bytes)> Files = [];
    public List<VehicleTrip> Trips = [];
    public List<VehicleMileageEntry> Mileages = [];
    public List<FuelClaim> Claims = [];
    public List<ApprovalDecision> Decisions = [];
    public Policy Policy => Policies.Single(p => p.Code == "TEST-POLICY");
    public VehicleFuelService Service;
    public SuccessionFixture()
    {
        Grants.Add(new(Permission, true, SystemAssignment, AccessScopeKind.All, null));
        Grants.Add(new(VehicleFuelPermissions.Self, false, Guid.NewGuid(), AccessScopeKind.All, null));
        Grants.Add(new(VehicleFuelPermissions.Fuel, true, Guid.NewGuid(), AccessScopeKind.All, null));
        var current = SuccessionProxy.Make<ICurrentAuthentication>((m, a) => Actor);
        var access = SuccessionProxy.Make<IAccountAccessReader>((m, a) => Task.FromResult<IReadOnlyList<PermissionGrant>>(
            Grants.Select(g => g with { AuthorityTimingKnown = true, ValidFromUtc = Clock.Now.AddYears(-1) }).ToArray()));
        var authz = SuccessionProxy.Make<IAccessManagementRepository>((m, a) => m.Name switch {
            "InTransactionAsync" => ((Delegate)a[0]!).DynamicInvoke(a[1]),
            "GetAssignmentRoleCodeAsync" => Task.FromResult<string?>("ADMIN"),
            "IsCurrentSystemRoleAssignmentAsync" => Task.FromResult(SystemRole && (Guid)a[1]! == SystemAssignment),
            _ => throw new Exception("Unexpected authorization: " + m.Name)
        });
        var auth = SuccessionProxy.Make<IAuthenticationRepository>((m, a) => throw new Exception(m.Name));
        var journal = SuccessionProxy.Make<ISchedulingJournalRepository>((m, a) => m.Name switch {
            "IsCurrentSessionAsync" => Task.FromResult(CurrentSession),
            "FindReceiptAsync" => Task.FromResult(Receipts.SingleOrDefault(x => x.ActorUserId == (string)a[0]! && x.Operation == (string)a[1]! && x.Key == (string)a[2]!)),
            "AddReceipt" => Add(Receipts, (IdempotencyReceipt)a[0]!), "AddAudit" => Add(Audits, (AuditEvent)a[0]!),
            "SaveChangesAsync" => Task.CompletedTask, _ => throw new Exception(m.Name)
        });
        var files = SuccessionProxy.Make<IPrivateFileStore>((m, a) => {
            if (m.Name == "WriteAsync") {
                var bytes = (byte[])a[0]!; var key = Guid.NewGuid().ToString("N"); Files[key] = ((string)a[1]!, bytes);
                return Task.FromResult(new PrivateFileReference(key, "1", Convert.ToHexString(SHA256.HashData(bytes)), "synthetic", bytes.Length));
            }
            if (BlockFileReads) throw new AuthenticationFailure("private_storage_unavailable", 503);
            var item = Files[((PrivateFileReference)a[0]!).StorageKey];
            if (item.Purpose != (string)a[1]!) throw new Exception("Evidence purpose mismatch"); return Task.FromResult(item.Bytes);
        });
        var repo = SuccessionProxy.Make<IVehicleFuelRepository>((m, a) => m.Name switch {
            "FindPolicyAsync" => Task.FromResult(Policies.SingleOrDefault(p => p.Id == (Guid)a[0]! && p.Category == PolicyCategory.VehicleFuel)),
            "PolicyCodeExistsAsync" => Task.FromResult(Policies.Any(p => p.Code == (string)a[0]!)),
            "AddPolicy" => Add(Policies, (Policy)a[0]!), "AddPolicyVersion" => Add(Versions, (PolicyVersion)a[0]!),
            "LatestPolicyVersionNumberAsync" => Task.FromResult(Versions.Where(v => v.PolicyId == (Guid)a[0]!).Select(v => v.VersionNumber).DefaultIfEmpty().Max()),
            "ExpectPolicyVersion" => Expect((Policy)a[0]!, (byte[])a[1]!),
            "SaveChangesAsync" => Save(),
            "ListPolicyPeriodVersionsAsync" => Task.FromResult<IReadOnlyList<PolicyVersion>>(Versions.ToArray()),
            "HasPolicyPeriodOverlapAsync" => Task.FromResult(Versions.Any(v => v.EffectiveFrom < (DateOnly)a[1]! && (v.EffectiveTo is null || v.EffectiveTo > (DateOnly)a[0]!) && Policies.Single(p => p.Id == v.PolicyId).IsActive)),
            "ExistingPolicyEmployeesAsync" => Task.FromResult<IReadOnlyList<Guid>>(((IReadOnlyList<Guid>)a[0]!).Where(id => EmployeeExists && id == Employee).ToArray()),
            "ActivePolicyRoleCodesAsync" => Task.FromResult<IReadOnlyList<string>>(((IReadOnlyList<string>)a[0]!).Where(c => RoleActive && c == "HR-REVIEWER").ToArray()),
            "PagePoliciesAsync" => PagePolicies((int)a[0]!, (int)a[1]!),
            "PagePolicyVersionsAsync" => PageVersions((Guid)a[0]!, (int)a[1]!, (int)a[2]!),
            "FindPolicyVersionAsync" => Task.FromResult(Versions.SingleOrDefault(v => v.PolicyId == (Guid)a[0]! && v.Id == (Guid)a[1]!)),
            "GetActiveVehicleFuelPolicyVersionAsync" => Task.FromResult(Versions.SingleOrDefault(v => v.EffectiveFrom <= (DateOnly)a[0]! && (v.EffectiveTo is null || v.EffectiveTo > (DateOnly)a[0]!))),
            "FindVehicleFuelPolicyVersionAsync" => Task.FromResult(Versions.SingleOrDefault(v => v.Id == (Guid)a[0]!)),
            "ListVehicleAdjustmentRequestsAsync" => Task.FromResult<IReadOnlyList<ApprovalRequest>>([]),
            "FindTripAsync" => Task.FromResult(Trips.SingleOrDefault(t => t.Id == (Guid)a[0]!)),
            "FindApprovedMileageForTripAsync" => Task.FromResult(Mileages.SingleOrDefault(t => t.VehicleTripId == (Guid)a[0]!)),
            "HasClaimForTripAsync" => Task.FromResult(Claims.Any(c => c.VehicleTripId == (Guid)a[0]!)),
            "AddFuelClaim" => Add(Claims, (FuelClaim)a[0]!),
            "FindFuelClaimAsync" => Task.FromResult(Claims.SingleOrDefault(c => c.Id == (Guid)a[0]!)),
            "AddDecision" => Add(Decisions, (ApprovalDecision)a[0]!),
            _ => throw new Exception("Unexpected repository: " + m.Name)
        });
        object[] dependencies = [new AttendanceOperation(current, access, authz, journal, auth, Clock, files), repo, files,
            current, access, authz, journal, Clock];
        var constructor = typeof(VehicleFuelService).GetConstructors().Single();
        Service = (VehicleFuelService)constructor.Invoke(constructor.GetParameters()
            .Select(p => dependencies.Single(p.ParameterType.IsInstanceOfType)).ToArray());
    }
    public async Task<JsonObject> Call(string methodName, params object?[] supplied)
    {
        var method = typeof(VehicleFuelService).GetMethod(methodName)!; var parameters = method.GetParameters();
        var args = new object?[parameters.Length];
        for (var i = 0; i < args.Length; i++)
        {
            var type = parameters[i].ParameterType;
            args[i] = type == typeof(AuthRequestContext) ? Context : type == typeof(CancellationToken) ? CancellationToken.None :
                type.IsInstanceOfType(supplied[i]) ? supplied[i] : JsonSerializer.Deserialize(JsonSerializer.Serialize(supplied[i]), type);
        }
        Task task;
        try { task = (Task)method.Invoke(Service, args)!; }
        catch (TargetInvocationException ex) { throw ex.InnerException!; }
        await task;
        return JsonSerializer.SerializeToNode(task.GetType().GetProperty("Result")!.GetValue(task))!.AsObject();
    }
    public JsonObject Rules(DateOnly? from = null, DateOnly? to = null) => JsonSerializer.SerializeToNode(new {
        SchemaVersion = 1, StandardFuelKmPerLiter = 20m, ClaimFuelPricePerLiter = 100m, TuningKm = 180m, OverhaulKm = 800m,
        Workflow = new VehicleWorkflowPolicy(1,
            [new(Employee, from ?? From, to ?? To, ["ComplaintResolution", "FieldVisits"], "synthetic-duty-proof")],
            [new("WORKSHOP-A", from ?? From, to ?? To, [VehicleServiceKind.Tuning, VehicleServiceKind.EngineOverhaul], "synthetic-workshop-authority")],
            [new(VehicleServiceKind.Tuning, "EveryThreshold", "Fixed", 50, "PKR", ["Motorcycle"]),
             new(VehicleServiceKind.EngineOverhaul, "Once", "Maximum", 500, "PKR", ["Motorcycle"])],
            "Odometer", new(["HR-REVIEWER"], "synthetic-incident-authority"), "PKR")
    })!.AsObject();
    public Task<JsonObject> Validate(JsonObject? rules = null) => Call("ValidatePolicyAsync", new { EffectiveFrom = From, EffectiveTo = To, Rules = rules ?? Rules() });
    public Task<JsonObject> Create(string code = "TEST-POLICY", string key = "vehicle-create-001") =>
        Call("CreatePolicyAsync", new { Code = code, Name = "Synthetic vehicle policy", Reason = "Synthetic HR provisioning" }, key);
    public JsonObject PreviewRequest(DateOnly? from = null, DateOnly? to = null, JsonObject? rules = null) => JsonSerializer.SerializeToNode(new {
        ExpectedVersion = Convert.ToBase64String(Policy.RowVersion), ExpectedRevision = Policy.Revision,
        EffectiveFrom = from ?? From, EffectiveTo = to ?? To, Rules = rules ?? Rules(from, to)
    })!.AsObject();
    public async Task<JsonObject> Prepare(DateOnly? from = null, DateOnly? to = null, JsonObject? rules = null)
    {
        if (Policies.Count == 0) await Create();
        var request = PreviewRequest(from, to, rules); var preview = await Call("PreviewPolicyAsync", Policy.Id, request);
        if (!preview["CanPublish"]!.GetValue<bool>()) throw new Exception(preview.ToJsonString());
        request["ExpectedPreviewFingerprint"] = preview["PreviewFingerprint"]!.DeepClone(); request["Reason"] = "Synthetic reviewed policy publication";
        return request;
    }
    public Task<JsonObject> Publish(JsonObject request, string key = "vehicle-publish-001") => Call("PublishPolicyAsync", Policy.Id, request, key);
    public void AddExistingVersion(DateOnly from, DateOnly? to)
    {
        var other = new Policy("OTHER-" + Guid.NewGuid().ToString("N"), "Existing HR vehicle policy", PolicyCategory.VehicleFuel); Policies.Add(other);
        Versions.Add(new(other.Id, 1, from, to, "legacy", "{}", null, null, "synthetic-publisher", Clock.Now, PolicyAuthority.DirectHr));
    }
    public VehicleTrip AddApprovedTrip(PolicyVersion version)
    {
        var start = new DateTimeOffset(2026, 11, 8, 7, 0, 0, TimeSpan.Zero); var vehicle = Guid.NewGuid();
        var trip = new VehicleTrip(Employee, vehicle, VehicleJourneyPurpose.Outstation, start, start.AddHours(1), "A", "B", 100, 220,
            120, "route-proof", "work-proof", Actor.AccountId.ToString(), start.AddHours(2));
        trip.Submit(new(VehicleFuelService.TripRequestType, trip.Id, Actor.AccountId.ToString(), "trip-proof", 1, start.AddHours(2)));
        trip.ApprovalRequest!.Decide(ApprovalState.Approved, Guid.NewGuid().ToString(), "HR-REVIEWER", "Synthetic approved trip", start.AddDays(1));
        Trips.Add(trip); Mileages.Add(new(vehicle, Employee, trip.Id, new(2026, 11, 8), 120, 40, 80, 0, "retained-mileage", version.Id,
            trip.ApprovalRequestId!.Value, "synthetic-mileage-001", "HR-REVIEWER", start.AddDays(1)));
        return trip;
    }
    private Task<(IReadOnlyList<Policy>, int)> PagePolicies(int skip, int size)
    { var rows = Policies.Where(p => p.Category == PolicyCategory.VehicleFuel).OrderBy(p => p.Code).ToArray(); return Task.FromResult(((IReadOnlyList<Policy>)rows.Skip(skip).Take(size).ToArray(), rows.Length)); }
    private Task<(IReadOnlyList<PolicyVersion>, int)> PageVersions(Guid id, int skip, int size)
    { var rows = Versions.Where(v => v.PolicyId == id).OrderByDescending(v => v.VersionNumber).ToArray(); return Task.FromResult(((IReadOnlyList<PolicyVersion>)rows.Skip(skip).Take(size).ToArray(), rows.Length)); }
    private object? Expect(Policy policy, byte[] expected)
    { if (!policy.RowVersion.SequenceEqual(expected)) throw new Exception("Concurrency expectation differs"); ExpectedVersions++; return null; }
    private Task Save()
    { foreach (var policy in Policies) typeof(Policy).GetProperty("RowVersion")!.SetValue(policy, BitConverter.GetBytes((long)++Stamp)); return Task.CompletedTask; }
    private static object? Add<T>(List<T> list, T value) { list.Add(value); return null; }
}


