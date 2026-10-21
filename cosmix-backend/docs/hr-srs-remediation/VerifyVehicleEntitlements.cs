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
using Cosmix.Domain.Common.Idempotency;
using Cosmix.Domain.HR.Policies;
using Cosmix.Domain.HR.VehicleFuel;

// Actual service/authorization/receipt code; all data, transactions and private storage are synthetic.
// This suite deliberately uses unchanged public service APIs so it can also expose frozen-baseline defects.
var passed = 0; var failures = new List<string>();
async Task Test(string name, Func<Task> action)
{
    try { await action(); passed++; Console.WriteLine("PASS " + name); }
    catch (Exception ex) { failures.Add(name + ": " + ex.GetType().Name + " " + ex.Message); Console.WriteLine("FAIL " + failures[^1]); }
}
void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
async Task Reject(Func<Task> action, string code)
{
    try { await action(); } catch (AuthenticationFailure ex) when (ex.Code == code) { return; }
    throw new Exception("Expected " + code);
}
await Test("trip without dated custody is denied", async () => {
    var f = new VehicleFixture(); f.Custodies.Clear();
    await Reject(() => f.SubmitTrip(), "vehicle_custody_not_valid_for_date");
    Check(f.Trips.Count == 0 && f.Receipts.Count == 0, "No successful write/receipt");
});
await Test("trip crossing custody transfer is denied", async () => {
    var f = new VehicleFixture(); f.Custodies[0].RecordReturn(f.Start.AddMinutes(30), 500, "return", "receiver");
    f.Custodies.Add(new(f.Vehicle.Id, Guid.NewGuid(), f.Start.AddMinutes(30), 500, "transfer", "custodian"));
    await Reject(() => f.SubmitTrip(), "vehicle_custody_not_valid_for_date");
});
await Test("historical returned custody permits its own trip and exact receipt replay", async () => {
    var f = new VehicleFixture(); f.Custodies[0].RecordReturn(f.Start.AddDays(1), 500, "return", "receiver");
    var first = await f.SubmitTrip(); var replay = await f.SubmitTrip();
    Check(first.Id == replay.Id && f.Trips.Count == 1 && first.ApprovalState == "Pending", "Retained request replay");
});
await Test("service without dated custody is denied", async () => {
    var f = new VehicleFixture(); f.Custodies.Clear();
    await Reject(() => f.SubmitService(), "vehicle_custody_not_valid_for_date");
});
await Test("maintenance stores authoritative as-of mileage not request mileage", async () => {
    var f = new VehicleFixture(); var result = await f.SubmitService();
    Check(result.ApprovedOutstationKm == 180, "Expected retained 180km, not supplied 999km or future 900km");
    Check(f.Records.Single().PolicyVersionId == f.Policy.Id, "Expected effective historical policy");
});
await Test("tuning only requires its configured milestone", async () => {
    var f = new VehicleFixture(); f.Policy = f.MakePolicy(1, new(2026, 10, 1), null, "{\"TuningKm\":180}");
    var result = await f.SubmitService(); Check(result.ApprovedOutstationKm == 180, "Inclusive configured boundary");
});
await Test("maintenance rejects one below configured milestone", async () => {
    var f = new VehicleFixture { HistoricalMileage = 179 };
    await Reject(() => f.SubmitService(), "maintenance_milestone_not_reached:179km_of_180km");
});
await Test("fuel uses retained approved distance and historical policy", async () => {
    var f = new VehicleFixture(); var trip = f.ApprovedTrip(); f.Mileage(trip, 80, 40);
    var result = await f.Service.SubmitFuelClaimAsync(new(trip.Id, "PKR", "receipt"), "fuel-submit-001", f.Context, default);
    Check(result.EligibleDistanceKm == 80 && result.ExcludedCommuteKm == 40, "Retained 80/40, not newly recomputed 90/30");
    Check(result.FuelLiters == 4 && result.ClaimedAmount == 400, "80 / configured20 * configured100 = 400");
    Check(f.Claims.Single().PolicyVersionId == f.Policy.Id, "Pinned historical policy, not newer version");
});
await Test("fuel without retained approved mileage is denied", async () => {
    var f = new VehicleFixture(); var trip = f.ApprovedTrip();
    await Reject(() => f.Service.SubmitFuelClaimAsync(new(trip.Id, "PKR", null), "fuel-submit-001", f.Context, default), "approved_trip_mileage_unavailable");
});
await Test("fuel without configured price has no invented default", async () => {
    var f = new VehicleFixture(); f.Policy = f.MakePolicy(1, new(2026,10,1), null, "{\"StandardFuelKmPerLiter\":20}");
    f.FuturePolicy = f.Policy; var trip = f.ApprovedTrip(); f.Mileage(trip, 80, 40);
    await Reject(() => f.Service.SubmitFuelClaimAsync(new(trip.Id, "PKR", null), "fuel-submit-001", f.Context, default), "vehicle_fuel_policy_not_configured");
});
await Test("trip approval cannot treat missing commute as zero", async () => {
    var f = new VehicleFixture(); var trip = f.PendingTrip(); f.Commute = null; f.Reviewer();
    await Reject(() => f.Service.ApproveTripAsync(trip.Id, "trip-approve-001", f.Context, default), "vehicle_commute_distance_not_configured");
    Check(trip.ApprovalRequest!.State == ApprovalState.Pending && f.Decisions.Count == 0, "Failed validation leaves decision pending");
});
await Test("positive work trip needs map distance before approval", async () => {
    var f = new VehicleFixture(); var trip = f.PendingTrip(mapKm: 0); f.Reviewer();
    await Reject(() => f.Service.ApproveTripAsync(trip.Id, "trip-approve-001", f.Context, default), "vehicle_map_distance_required");
});
await Test("trip approval retains local business date and actual reviewer role", async () => {
    var f = new VehicleFixture(); f.Start = new(2026, 10, 8, 22, 0, 0, TimeSpan.Zero);
    var trip = f.PendingTrip(); f.Reviewer(); await f.Service.ApproveTripAsync(trip.Id, "trip-approve-001", f.Context, default);
    Check(f.Mileages.Single().BusinessDate == new DateOnly(2026,10,9), "Karachi 03:00 is October9, not UTC October8");
    Check(f.Decisions.Single().ActorRoleCode == "Configured-Reviewer", "Actual authorized role, not hardcoded HR");
});
await Test("same employee cannot approve through another account", async () => {
    var f = new VehicleFixture(); var trip = f.PendingTrip(); f.Actor = f.Actor with { AccountId = Guid.NewGuid() };
    await Reject(() => f.Service.ApproveTripAsync(trip.Id, "trip-approve-001", f.Context, default), "vehicle_self_decision_forbidden");
});
await Test("future incident is not a recorded past event", async () => {
    var f = new VehicleFixture(); await Reject(() => f.Incident(f.Now.AddDays(1)), "incident_in_future");
});
await Test("incident amount/currency mismatch is a controlled validation failure", async () => {
    var f = new VehicleFixture(); await Reject(() => f.Incident(f.Start, amount: 100, currency: null), "incident_liability_currency_required");
});
await Test("incident outside retained custody is denied", async () => {
    var f = new VehicleFixture(); await Reject(() => f.Incident(f.Start.AddDays(-8)), "incident_outside_custody_window");
});
await Test("incident proposed liability and insurer reference remain pending, not settled", async () => {
    var f = new VehicleFixture(); var result = await f.Incident(f.Start, amount: 75, currency: "PKR");
    Check(result.ApprovalState == "Pending" && result.ProposedEmployeeLiability == 75 && f.Decisions.Count == 0,
        "No financial decision, deduction, payment or insurer settlement");
});
await Test("missing self-service permission denies before repository write", async () => {
    var f = new VehicleFixture(); f.Grants.Clear(); await Reject(() => f.SubmitTrip(), "access_denied");
    Check(f.Trips.Count == 0 && f.Receipts.Count == 0, "Denied before mutation");
});
await Test("missing workflow configuration fails closed", async () => {
    var f = new VehicleFixture(); f.Policy = f.MakePolicy(1,new(2026,10,1),null,"{\"TuningKm\":180}",false);
    await Reject(() => f.SubmitService(), "vehicle_workflow_policy_not_configured");
});
await Test("staff eligibility is dated and employee specific", async () => {
    var f = new VehicleFixture(); f.ChangeWorkflow(w => w["StaffAssignments"]![0]!["EmployeeId"] = Guid.NewGuid());
    await Reject(() => f.SubmitTrip(), "vehicle_staff_not_eligible_for_date");
    Check(f.Trips.Count == 0 && f.Receipts.Count == 0, "No ineligible entitlement request");
});
await Test("eligibility expiry is exclusive", async () => {
    var f = new VehicleFixture(); f.ChangeWorkflow(w => w["StaffAssignments"]![0]!["EffectiveTo"] = "2026-10-09");
    await Reject(() => f.SubmitService(), "vehicle_staff_not_eligible_for_date");
});
await Test("overlapping duty evidence cannot silently select one assignment", async () => {
    var f = new VehicleFixture(); f.ChangeWorkflow(w => w["StaffAssignments"]!.AsArray().Add(w["StaffAssignments"]![0]!.DeepClone()));
    await Reject(() => f.SubmitTrip(), "vehicle_staff_not_eligible_for_date");
});
await Test("workshop authority is required at service date", async () => {
    var f = new VehicleFixture(); f.ChangeWorkflow(w => w["Workshops"]![0]!["EffectiveTo"] = "2026-10-09");
    await Reject(() => f.SubmitService(), "vehicle_workshop_not_authorized_for_date");
});
await Test("unconfigured workshops have no implied authority", async () => {
    var f = new VehicleFixture(); f.ChangeWorkflow(w => w["Workshops"] = null);
    await Reject(() => f.SubmitService(), "vehicle_maintenance_rules_not_configured");
});
await Test("fixed amount cannot be increased by claimant", async () => {
    var f = new VehicleFixture();
    await Reject(() => f.SubmitService(amount: 51), "vehicle_maintenance_amount_outside_policy");
});
await Test("other expenses cannot bypass configured maintenance kinds", async () => {
    var f = new VehicleFixture();
    await Reject(() => f.SubmitService(kind: VehicleServiceKind.Other), "vehicle_service_cost_not_covered");
});
await Test("maintenance source change blocks approval", async () => {
    var f = new VehicleFixture(); var row = await f.SubmitService(); f.HistoricalMileage = 181; f.Reviewer();
    await Reject(() => f.ApproveService(row.Id), "vehicle_service_eligibility_changed");
    Check(f.Records.Single().ApprovalRequest!.State == ApprovalState.Pending && f.Decisions.Count == 0, "Stale source is not approved");
});
await Test("two pending claims cannot consume the same mileage threshold", async () => {
    var f = new VehicleFixture(); var first = await f.SubmitService();
    var second = await f.SubmitService(evidence:"different-service-proof",key:"service-submit-002"); f.Reviewer();
    await f.ApproveService(first.Id);
    await Reject(() => f.ApproveService(second.Id,"service-approve-002"), "maintenance_milestone_not_reached:180km_of_360km");
    Check(f.Decisions.Count == 1 && f.Records[1].ApprovalRequest!.State == ApprovalState.Pending, "Only one cycle decision");
});
await Test("service evidence cannot be reused under another key", async () => {
    var f = new VehicleFixture(); await f.SubmitService();
    await Reject(() => f.SubmitService(key:"service-submit-002"), "vehicle_service_evidence_already_used");
});
await Test("next configured threshold remains usable after one approved cycle", async () => {
    var f = new VehicleFixture(); var first = await f.SubmitService(); var employeeActor = f.Actor;
    f.Reviewer(); var approved = await f.ApproveService(first.Id); var replay = await f.ApproveService(first.Id);
    Check(approved.Id == replay.Id && f.Decisions.Count == 1, "Exact approval retry does not consume another cycle");
    f.Actor = employeeActor; f.HistoricalMileage = 360;
    var second = await f.SubmitService(evidence:"next-service-proof",key:"service-submit-002");
    f.Reviewer(); await f.ApproveService(second.Id,"service-approve-002");
    Check(f.Decisions.Count == 2 && f.Records.All(r => r.ApprovalRequest!.State == ApprovalState.Approved), "Two distinct retained cycles");
});
await Test("site-to-site mileage uses independently approved map distance", async () => {
    var f = new VehicleFixture(); var trip = f.PendingTrip(mapKm:80,purpose:VehicleJourneyPurpose.SiteToSite); f.Reviewer();
    await f.Service.ApproveTripAsync(trip.Id,"trip-approve-001",f.Context,default);
    Check(f.Mileages.Single().ApprovedBusinessKm == 80 && f.Mileages.Single().ExcludedCommuteKm == 0,
        "Map 80 retained independently of odometer 120");
});
await Test("unspecified non-site distance basis cannot invent valuation", async () => {
    var f = new VehicleFixture(); f.ChangeWorkflow(w => w["OtherWorkDistanceBasis"] = null);
    var trip = f.PendingTrip(); f.Reviewer();
    await Reject(() => f.Service.ApproveTripAsync(trip.Id,"trip-approve-001",f.Context,default),"vehicle_distance_basis_not_configured");
});
await Test("fuel currency must match configured price currency", async () => {
    var f = new VehicleFixture(); var trip = f.ApprovedTrip(); f.Mileage(trip,80,40);
    await Reject(() => f.Service.SubmitFuelClaimAsync(new(trip.Id,"USD",null),"fuel-submit-001",f.Context,default),"vehicle_fuel_currency_mismatch");
});
await Test("incident review requires configured authority even with permission", async () => {
    var f = new VehicleFixture(); var row = await f.Incident(f.Start); f.ChangeWorkflow(w => w["IncidentReview"] = null); f.Reviewer();
    await Reject(() => f.ReviewIncident(row),"vehicle_incident_authority_not_configured");
    Check(f.Incidents.Single().ApprovalRequest!.State == ApprovalState.Pending,"No authority leaves pending");
});
await Test("incident reviewer role must match configured authority", async () => {
    var f = new VehicleFixture(); var row = await f.Incident(f.Start);
    f.ChangeWorkflow(w => w["IncidentReview"]!["RoleCodes"] = new JsonArray("Different-Reviewer")); f.Reviewer();
    await Reject(() => f.ReviewIncident(row),"access_denied");
});
await Test("incident self review is denied across accounts", async () => {
    var f = new VehicleFixture(); var row = await f.Incident(f.Start); f.Actor = f.Actor with { AccountId = Guid.NewGuid() };
    await Reject(() => f.ReviewIncident(row),"vehicle_self_decision_forbidden");
});
await Test("incident reviewed proposal is retained and never authorizes settlement", async () => {
    var f = new VehicleFixture(); var row = await f.Incident(f.Start); f.Reviewer();
    var reviewed = await f.ReviewIncident(row); var replay = await f.ReviewIncident(row);
    var read = await f.Service.ReadIncidentReviewAsync(row.Id,f.Context,default);
    Check(reviewed.DecisionId == replay.DecisionId && read == reviewed && f.Decisions.Count == 1,"Durable decision/read/retry contract");
    Check(!reviewed.EmployeeDeductionAuthorized && !reviewed.InsuranceSettled && reviewed.ProposedEmployeeLiability == 75,
        "Proposal remains separate from collection, deduction or insurer settlement");
    await Reject(() => f.ReviewIncident(row,"incident-review-002"),"vehicle_incident_not_pending");
});
await Test("surplus mileage cannot silently rebase a second pending cycle", async () => {
    var f = new VehicleFixture { HistoricalMileage = 360 }; var first = await f.SubmitService();
    var second = await f.SubmitService(evidence:"second-proof",key:"service-submit-002"); f.Reviewer();
    await f.ApproveService(first.Id);
    await Reject(() => f.ApproveService(second.Id,"service-approve-002"),"vehicle_service_eligibility_changed");
});
await Test("configured once-only overhaul cannot repeat despite surplus mileage", async () => {
    var f = new VehicleFixture { HistoricalMileage = 800 }; var employeeActor = f.Actor;
    var first = await f.SubmitService(amount:450,kind:VehicleServiceKind.EngineOverhaul); f.Reviewer();
    await f.ApproveService(first.Id); f.Actor = employeeActor; f.HistoricalMileage = 1600;
    await Reject(() => f.SubmitService(amount:450,kind:VehicleServiceKind.EngineOverhaul,evidence:"another-overhaul",key:"service-submit-002"),
        "vehicle_service_once_already_consumed");
});
await Test("changed mileage thresholds need an explicit transition rule", async () => {
    var f = new VehicleFixture(); var employeeActor = f.Actor; var first = await f.SubmitService();
    f.Reviewer(); await f.ApproveService(first.Id); f.Actor = employeeActor; f.HistoricalMileage = 1000;
    var rules = JsonNode.Parse(f.Policy.RulesJson)!.AsObject(); rules["TuningKm"] = 200;
    f.Policy = f.MakePolicy(2,new(2026,10,1),null,rules.ToJsonString());
    await Reject(() => f.SubmitService(evidence:"later-proof",key:"service-submit-002"),"vehicle_service_cycle_policy_transition_unconfigured");
});
await Test("unknown monthly adjustment rules cannot affect maintenance silently", async () => {
    var f = new VehicleFixture(); typeof(VehicleMileageEntry).GetProperty("MonthlyAdjustmentKm")!.SetValue(f.ServiceMileage,1m);
    await Reject(() => f.SubmitService(),"vehicle_mileage_source_invalid");
});
await Test("legacy maintenance evidence cannot be treated as a known unconsumed cycle", async () => {
    var f = new VehicleFixture(); var row = await f.SubmitService();
    var reference = JsonSerializer.Deserialize<PrivateFileReference>(f.Records.Single().EligibilityCalculationPrivateReference)!;
    var original = f.Files[reference.StorageKey]; f.Files[reference.StorageKey] = (original.Purpose,JsonSerializer.SerializeToUtf8Bytes(new { SchemaVersion = 1 }));
    f.Reviewer(); await Reject(() => f.ApproveService(row.Id),"vehicle_service_cycle_evidence_unavailable");
});
await Test("incident return and reject outcomes retain their separate approval states", async () => {
    foreach (var outcome in new[] { VehicleIncidentReviewOutcome.ReturnedForEvidence, VehicleIncidentReviewOutcome.Rejected })
    {
        var f = new VehicleFixture(); var row = await f.Incident(f.Start); f.Reviewer();
        var result = await f.Service.ReviewIncidentAsync(row.Id,new(row.ApprovalRequestId!.Value,outcome,
            "review-proof","Evidence reviewed",null,null,null),"incident-review-001",f.Context,default);
        Check(result.ApprovalState == (outcome == VehicleIncidentReviewOutcome.Rejected ? "Rejected" : "Returned"),"State matches outcome");
        Check(!result.EmployeeDeductionAuthorized && !result.InsuranceSettled,"No financial authority");
    }
});
await Test("insurance review outcome requires insurer evidence reference", async () => {
    var f = new VehicleFixture(); var row = await f.Incident(f.Start); f.Reviewer();
    await Reject(() => f.Service.ReviewIncidentAsync(row.Id,new(row.ApprovalRequestId!.Value,VehicleIncidentReviewOutcome.InsuranceReviewRequired,
        "review-proof","Insurance review",null,null,null),"incident-review-001",f.Context,default),"vehicle_incident_insurance_reference_required");
});
await Test("incident review replay rechecks configured role authority", async () => {
    var f = new VehicleFixture(); var row = await f.Incident(f.Start); f.Reviewer(); await f.ReviewIncident(row);
    f.ReviewerRole = "Another-Permission-Holder";
    await Reject(() => f.ReviewIncident(row),"access_denied");
    Check(f.Decisions.Count == 1,"Revocation cannot create or disclose a new successful review receipt");
});
Console.WriteLine($"Vehicle G3: {passed}/{passed + failures.Count} scenarios passed; synthetic collaborators; no SQL/provider/host calls.");
if (failures.Count > 0) Environment.ExitCode = 1;

public interface IVehicleFixtureRepository : IVehicleFuelRepository, IVehicleFuelMileageWriter { }
public class VehicleProxy : DispatchProxy
{
    public Func<MethodInfo, object?[], object?> Handler = null!;
    protected override object? Invoke(MethodInfo? method, object?[]? args) => Handler(method!, args ?? []);
    public static T Make<T>(Func<MethodInfo, object?[], object?> handler) where T : class
    { var value = Create<T, VehicleProxy>(); ((VehicleProxy)(object)value).Handler = handler; return value; }
}
public sealed class VehicleClock : TimeProvider
{ public override DateTimeOffset GetUtcNow() => new(2026,10,15,7,0,0,TimeSpan.Zero); }
public sealed class VehicleFixture
{
    public Guid Employee = Guid.NewGuid();
    public AuthenticatedAccount Actor = new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 1, SessionClient.Web, true);
    public DateTimeOffset Start = new(2026,10,9,7,0,0,TimeSpan.Zero);
    public DateTimeOffset Now => new VehicleClock().GetUtcNow();
    public Vehicle Vehicle = new("TEST-G3", "Motorcycle", "Synthetic", null);
    public PolicyVersion Policy, FuturePolicy;
    public decimal HistoricalMileage = 180;
    public VehicleMileageEntry ServiceMileage;
    public EmployeeCommuteDistance? Commute;
    public List<VehicleCustody> Custodies = [];
    public List<VehicleTrip> Trips = [];
    public List<VehicleMileageEntry> Mileages = [];
    public List<VehicleServiceRecord> Records = [];
    public List<FuelClaim> Claims = [];
    public List<VehicleIncident> Incidents = [];
    public List<ApprovalDecision> Decisions = [];
    public List<IdempotencyReceipt> Receipts = [];
    public List<PermissionGrant> Grants = [];
    public string ReviewerRole = "Configured-Reviewer";
    public Dictionary<string, (string Purpose, byte[] Bytes)> Files = [];
    public AuthRequestContext Context = new("127.0.0.1", "synthetic-vehicle-g3");
    public VehicleFuelService Service;
    public VehicleFixture()
    {
        Actor = Actor with { EmployeeId = Employee };
        Custodies.Add(new(Vehicle.Id, Employee, Start.AddDays(-7), 0, "issue-proof", "custodian"));
        Commute = new(Employee, 30, "route-proof", new(2026,10,1), null, "HR", Now.AddDays(-14));
        Policy = MakePolicy(1, new(2026,10,1), new(2026,11,1), "{\"StandardFuelKmPerLiter\":20,\"ClaimFuelPricePerLiter\":100,\"TuningKm\":180,\"OverhaulKm\":800}");
        FuturePolicy = MakePolicy(2, new(2026,11,1), null, "{\"StandardFuelKmPerLiter\":10,\"ClaimFuelPricePerLiter\":900,\"TuningKm\":180,\"OverhaulKm\":800}");
        ServiceMileage = new(Vehicle.Id,Employee,Guid.NewGuid(),new(2026,10,8),180,0,180,0,"retained-service-mileage",
            Policy.Id,Guid.NewGuid(),"service-source-001","reviewer",Now.AddDays(-6));
        foreach (var p in VehicleFuelPermissions.Definitions) Grants.Add(new(p.Code, p.Sensitive, Guid.NewGuid(), AccessScopeKind.All, null));
        var current = VehicleProxy.Make<ICurrentAuthentication>((m,a) => Actor);
        var access = VehicleProxy.Make<IAccountAccessReader>((m,a) => Task.FromResult<IReadOnlyList<PermissionGrant>>(
            Grants.Select(g => g with { AuthorityTimingKnown = true, ValidFromUtc = Now.AddYears(-1) }).ToArray()));
        var authz = VehicleProxy.Make<IAccessManagementRepository>((m,a) => m.Name switch {
            "InTransactionAsync" => ((Delegate)a[0]!).DynamicInvoke(a[1]),
            "GetAssignmentRoleCodeAsync" => Task.FromResult<string?>(ReviewerRole), _ => throw new Exception(m.Name) });
        var auth = VehicleProxy.Make<IAuthenticationRepository>((m,a) => throw new Exception(m.Name));
        var journal = VehicleProxy.Make<ISchedulingJournalRepository>((m,a) => m.Name switch {
            "IsCurrentSessionAsync" => Task.FromResult(true),
            "FindReceiptAsync" => Task.FromResult(Receipts.SingleOrDefault(x => x.ActorUserId == (string)a[0]! && x.Operation == (string)a[1]! && x.Key == (string)a[2]!)),
            "AddReceipt" => Add(Receipts,(IdempotencyReceipt)a[0]!), "AddAudit" => null, "SaveChangesAsync" => Task.CompletedTask,
            _ => throw new Exception(m.Name) });
        var files = VehicleProxy.Make<IPrivateFileStore>((m,a) => {
            if (m.Name == "WriteAsync") {
                var bytes = (byte[])a[0]!; var id = Guid.NewGuid().ToString("N"); Files[id] = ((string)a[1]!,bytes);
                return Task.FromResult(new PrivateFileReference(id,"1",Convert.ToHexString(SHA256.HashData(bytes)),"synthetic",bytes.Length));
            }
            var file = Files[((PrivateFileReference)a[0]!).StorageKey];
            if (file.Purpose != (string)a[1]!) throw new Exception("Private purpose mismatch"); return Task.FromResult(file.Bytes);
        });
        var repo = VehicleProxy.Make<IVehicleFixtureRepository>((m,a) => m.Name switch {
            "FindVehicleAsync" => Task.FromResult<Vehicle?>(Vehicle),
            "FindCustodyAsync" => Task.FromResult(Custodies.SingleOrDefault(x => x.Id == (Guid)a[0]!)),
            "ListCustodyHistoryAsync" => Task.FromResult<IReadOnlyList<VehicleCustody>>(Custodies.ToArray()),
            "FindCustodiesOverlappingAsync" => Task.FromResult<IReadOnlyList<VehicleCustody>>(Custodies.Where(x => x.IssuedAt < (DateTimeOffset)a[2]! && (!x.ReturnedAt.HasValue || x.ReturnedAt > (DateTimeOffset)a[1]!)).ToArray()),
            "FindActiveCommuteDistanceAsync" => Task.FromResult(Commute),
            "GetActiveVehicleFuelPolicyVersionAsync" => Task.FromResult<PolicyVersion?>(a.Length == 1 ? FuturePolicy : Policy),
            "FindVehicleFuelPolicyVersionAsync" => Task.FromResult<PolicyVersion?>(Policy.Id == (Guid)a[0]! ? Policy : null),
            "GetAccumulatedOutstationMileageAsync" => Task.FromResult(a.Length == 2 ? 900m : HistoricalMileage),
            "ListApprovedMileageAsync" => ServiceMileageRows(),
            "ListVehicleAdjustmentRequestsAsync" => Task.FromResult<IReadOnlyList<ApprovalRequest>>([]),
            "FindLatestApprovedServiceAsync" => Task.FromResult(Records.LastOrDefault(x => x.VehicleId == (Guid)a[0]! &&
                x.ServiceKind == (VehicleServiceKind)a[1]! && x.ApprovalRequest?.State == ApprovalState.Approved)),
            "HasServiceEvidenceAsync" => Task.FromResult(Records.Any(x => x.VehicleId == (Guid)a[0]! && x.EvidencePrivateReference == (string)a[1]!)),
            "FindApprovedMileageForTripAsync" => Task.FromResult(Mileages.SingleOrDefault(x => x.VehicleTripId == (Guid)a[0]!)),
            "FindTripAsync" => Task.FromResult(Trips.SingleOrDefault(x => x.Id == (Guid)a[0]!)),
            "HasTripOverlapAsync" => Task.FromResult(Trips.Any(t => t.VehicleId == (Guid)a[0]! && t.Id != (Guid?)a[5] &&
                t.ApprovalRequest?.State is ApprovalState.Pending or ApprovalState.Approved &&
                (t.StartedAt < (DateTimeOffset)a[2]! && t.EndedAt > (DateTimeOffset)a[1]! ||
                 t.OdometerStartKm < t.OdometerEndKm && (decimal)a[3]! < (decimal)a[4]! && t.OdometerStartKm < (decimal)a[4]! && t.OdometerEndKm > (decimal)a[3]!))),
            "HasClaimForTripAsync" => Task.FromResult(Claims.Any(x => x.VehicleTripId == (Guid)a[0]!)),
            "FindServiceRecordAsync" => Task.FromResult(Records.SingleOrDefault(x => x.Id == (Guid)a[0]!)),
            "FindFuelClaimAsync" => Task.FromResult(Claims.SingleOrDefault(x => x.Id == (Guid)a[0]!)),
            "FindIncidentAsync" => Task.FromResult(Incidents.SingleOrDefault(x => x.Id == (Guid)a[0]!)),
            "FindIncidentDecisionAsync" => Task.FromResult(Decisions.SingleOrDefault(x => x.ApprovalRequestId == (Guid)a[0]!)),
            "AddTrip" => Add(Trips,(VehicleTrip)a[0]!), "AddMileageEntry" => Add(Mileages,(VehicleMileageEntry)a[0]!),
            "AddServiceRecord" => Add(Records,(VehicleServiceRecord)a[0]!), "AddFuelClaim" => Add(Claims,(FuelClaim)a[0]!),
            "AddIncident" => Add(Incidents,(VehicleIncident)a[0]!), "AddDecision" => Add(Decisions,(ApprovalDecision)a[0]!),
            "SaveChangesAsync" => Task.CompletedTask, _ => throw new Exception("Unexpected repository method " + m.Name)
        });
        var clock = new VehicleClock();
        object[] dependencies = [new AttendanceOperation(current, access, authz, journal, auth, clock, files), repo, files,
            current, access, authz, journal, clock];
        var constructor = typeof(VehicleFuelService).GetConstructors().Single();
        Service = (VehicleFuelService)constructor.Invoke(constructor.GetParameters()
            .Select(p => dependencies.Single(p.ParameterType.IsInstanceOfType)).ToArray());
    }
    public PolicyVersion MakePolicy(int version, DateOnly from, DateOnly? to, string rules, bool workflow = true)
    {
        var json = JsonNode.Parse(rules)!.AsObject();
        if (workflow && !json.ContainsKey("Workflow")) json["Workflow"] = JsonSerializer.SerializeToNode(new VehicleWorkflowPolicy(1,
            [new(Employee,new(2026,10,1),null,["ComplaintResolution","FieldVisits"],"staff-duty-proof")],
            [new("workshop-proof",new(2026,10,1),null,[VehicleServiceKind.Tuning,VehicleServiceKind.EngineOverhaul],"workshop-authority-proof")],
            [new(VehicleServiceKind.Tuning,"EveryThreshold","Fixed",50,"PKR",["Motorcycle"]),
                new(VehicleServiceKind.EngineOverhaul,"Once","Maximum",500,"PKR",["Motorcycle"])],
            "Odometer",new(["Configured-Reviewer"],"incident-authority-proof"),"PKR"));
        return new(Guid.NewGuid(),version,from,to,"synthetic-vehicle-v1",json.ToJsonString(),"policy-proof",null,"publisher",Now.AddDays(-14),PolicyAuthority.DirectHr);
    }
    public void ChangeWorkflow(Action<JsonObject> change)
    {
        var rules = JsonNode.Parse(Policy.RulesJson)!.AsObject(); change(rules["Workflow"]!.AsObject());
        Policy = MakePolicy(1,Policy.EffectiveFrom,Policy.EffectiveTo,rules.ToJsonString());
    }
    private Task<IReadOnlyList<VehicleMileageEntry>> ServiceMileageRows()
    {
        typeof(VehicleMileageEntry).GetProperty("ApprovedBusinessKm")!.SetValue(ServiceMileage,HistoricalMileage);
        typeof(VehicleMileageEntry).GetProperty("OutstationKm")!.SetValue(ServiceMileage,HistoricalMileage);
        return Task.FromResult<IReadOnlyList<VehicleMileageEntry>>([ServiceMileage]);
    }
    public void Reviewer() => Actor = Actor with { AccountId = Guid.NewGuid(), EmployeeId = Guid.NewGuid() };
    public Task<VehicleTripResponse> SubmitTrip() => Service.SubmitTripAsync(new(Vehicle.Id,VehicleJourneyPurpose.Outstation,
        Start,Start.AddHours(1),"A","B",100,220,120,"route-proof","work-proof"),"trip-submit-001",Context,default);
    public Task<VehicleServiceRecordResponse> SubmitService(decimal amount = 50, VehicleServiceKind kind = VehicleServiceKind.Tuning,
        string evidence = "service-proof", string key = "service-submit-001") => Service.SubmitServiceRecordAsync(new(Vehicle.Id,kind,
        new(2026,10,9),500,999,"workshop-proof",evidence,amount,"PKR"),key,Context,default);
    public Task<VehicleServiceRecordResponse> ApproveService(Guid id, string key = "service-approve-001") =>
        Service.ApproveServiceRecordAsync(id,key,Context,default);
    public Task<VehicleIncidentReviewResponse> ReviewIncident(VehicleIncidentResponse row, string key = "incident-review-001") =>
        Service.ReviewIncidentAsync(row.Id,new(row.ApprovalRequestId!.Value,VehicleIncidentReviewOutcome.ProposedLiabilityRecorded,
            "review-proof","Recorded for authorized follow-up",75,"PKR","insurer-reference"),key,Context,default);
    public Task<VehicleIncidentResponse> Incident(DateTimeOffset at, decimal? amount = null, string? currency = null) =>
        Service.ReportIncidentAsync(new(Custodies[0].Id,at,"incident-proof",null,"insurer-reference",amount,currency),"incident-report-001",Context,default);
    public VehicleTrip PendingTrip(decimal mapKm = 120, VehicleJourneyPurpose purpose = VehicleJourneyPurpose.Outstation)
    {
        var trip = new VehicleTrip(Employee,Vehicle.Id,purpose,Start,Start.AddHours(1),"A","B",100,220,mapKm,
            "route-proof","work-proof",Actor.AccountId.ToString(),Now.AddHours(-1));
        trip.Submit(new(VehicleFuelService.TripRequestType,trip.Id,Actor.AccountId.ToString(),"trip-proof",1,Now.AddHours(-1)));
        Trips.Add(trip); return trip;
    }
    public VehicleTrip ApprovedTrip()
    {
        var trip = PendingTrip(); trip.ApprovalRequest!.Decide(ApprovalState.Approved,Guid.NewGuid().ToString(),"Reviewer","Approved",Now);
        return trip;
    }
    public void Mileage(VehicleTrip trip, decimal eligible, decimal excluded) => Mileages.Add(new(Vehicle.Id,Employee,trip.Id,new(2026,10,9),
        eligible+excluded,excluded,eligible,0,"protected-calculation",Policy.Id,trip.ApprovalRequestId!.Value,"mileage-key-001","Reviewer",Now));
    private static object? Add<T>(List<T> list,T item) { list.Add(item); return null; }
}
