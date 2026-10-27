using System.Data.Common;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using Cosmix.Api.Authentication;
using Cosmix.Application.Attendance.Common;
using Cosmix.Application.Authentication;
using Cosmix.Application.Common.Files;
using Cosmix.Application.HR.VehicleFuel;
using Cosmix.Domain.Authentication;
using Cosmix.Domain.Common.Approvals;
using Cosmix.Domain.HR.Policies;
using Cosmix.Domain.HR.VehicleFuel;
using Cosmix.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

// Real corpus constructors, SQL Server mappings and application services; adapters
// are explicitly in memory. This executable must never open SQL or write evidence.
var checks = 0;
void Check(bool ok, string label) { if (!ok) throw new Exception(label); checks++; Console.WriteLine("PASS " + label); }
var path = Environment.GetEnvironmentVariable("COSMIX_VEHICLE_FIXTURE_ASSEMBLY") ?? throw new Exception("Fixture assembly required.");
var hash = Environment.GetEnvironmentVariable("COSMIX_VEHICLE_FIXTURE_SHA256") ?? throw new Exception("Fixture hash required.");
Check(Path.GetFullPath(path).StartsWith(@"D:\CosmixVerification\", StringComparison.OrdinalIgnoreCase) &&
    Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))) == hash, "reviewed fixture input");
dynamic f = Activator.CreateInstance(Assembly.LoadFrom(path).GetType("DatedFixture", true)!)!;
var type = typeof(CurrentAuthentication).Assembly.GetType("Cosmix.Api.Development.HrVehicleV2Fixture", true)!;
var create = type.GetMethod("CreateRowsAsync", BindingFlags.NonPublic | BindingFlags.Static)!;
var checkRows = type.GetMethod("RequireExactTrackedRows", BindingFlags.NonPublic | BindingFlags.Static)!;
var ids = (Guid[])type.GetField("Ids", BindingFlags.NonPublic | BindingFlags.Static)!.GetValue(null)!;
var hr = (Guid)type.GetField("HrAccount", BindingFlags.NonPublic | BindingFlags.Static)!.GetValue(null)!;
var employee = (Guid)type.GetField("HrEmployee", BindingFlags.NonPublic | BindingFlags.Static)!.GetValue(null)!;
var admin = (Guid)type.GetField("AdminAccount", BindingFlags.NonPublic | BindingFlags.Static)!.GetValue(null)!;
var adminEmployee = Guid.Parse("7c505086-87ea-4fd2-980e-823ecccbe36a");
var superadmin = Guid.NewGuid();
var superEmployee = Guid.NewGuid();
f.Employee = employee; f.OwnerAccount = hr;
f.Clock.Now = new DateTimeOffset(2026, 10, 26, 10, 0, 0, TimeSpan.Zero);
f.Policies.Clear(); f.Custody.Clear(); f.Mileage.Clear();
f.Vehicle = new Vehicle("TEST-HR-V2-20261026", "Motorcycle", "TEST ONLY historical acceptance", null);
var noConnection = new NoSqlConnection();
var noCommand = new NoSqlCommand();
using var db = new CosmixDbContext(new DbContextOptionsBuilder<CosmixDbContext>()
    .UseSqlServer("Server=offline.invalid;Database=OfflineV2;Integrated Security=true")
    .AddInterceptors(noConnection, noCommand).Options);
var fileCount = 0;
Func<byte[], string, Task<string>> store = async (bytes, purpose) => {
    fileCount++;
    PrivateFileReference reference = await f.Store(bytes, purpose);
    return AttendanceOperation.Reference(reference);
};
var rows = await (Task<object[]>)create.Invoke(null, [db, (Guid)f.Vehicle.Id, (DateTimeOffset)f.Clock.Now, store])!;
Check(fileCount == 5 && rows.Length == 12, "exact twelve sources and five private evidence payloads");
Check(!db.ChangeTracker.Entries().Any(), "corpus generation leaves all entities detached");
var expectedTables = new[] { "Policies", "PolicyVersions", "PolicyVersions", "PolicyVersions", "VehicleCustodies",
    "VehicleTrips", "VehicleMileageEntries", "FuelClaims", "ApprovalRequests", "ApprovalRequests", "ApprovalDecisions", "ApprovalDecisions" };
for (var i = 0; i < rows.Length; i++) {
    var entry = db.Entry(rows[i]);
    Check((Guid)entry.Property("Id").CurrentValue! == ids[i] && entry.Metadata.GetTableName() == expectedTables[i], "exact source identity/table " + i);
}
foreach (var version in rows.OfType<PolicyVersion>()) {
    using var json = JsonDocument.Parse(version.RulesJson);
    var validation = VehiclePolicyRulesCodec.Validate(json.RootElement, version.EffectiveFrom, version.EffectiveTo);
    Check(validation.IsValid && validation.CanonicalJson == version.RulesJson, "canonical policy roundtrip " + version.VersionNumber);
    Check(VehicleAdjustmentPolicy.ValidJson(json.RootElement.GetProperty("Adjustments"), out _), "runtime policy accepts explicit nullable configuration " + version.VersionNumber);
    Check(version.PublishedAt == (DateTimeOffset)f.Clock.Now && version.Authority == PolicyAuthority.DirectHr &&
        version.ApprovalRequestId is null, "synthetic policy has actual preparation time, not fake workflow " + version.VersionNumber);
    f.Policies.Add(version);
}
db.AddRange(rows);
checkRows.Invoke(null, [db, rows]);
Check(db.ChangeTracker.Entries().Count() == 12, "EF relationship fixup adds exactly twelve approved rows");
Check(db.ChangeTracker.Entries().All(e => e.State == EntityState.Added), "no existing entity marked modified");
var trip = (VehicleTrip)rows[5]; var fuel = (FuelClaim)rows[7];
Check(trip.ApprovalRequest == rows[8] && fuel.ApprovalRequest == rows[9], "both subject/approval graph links exact");
foreach (var row in rows) switch (row) {
    case VehicleCustody value: f.Custody.Add(value); break;
    case VehicleTrip value: f.Trips.Add(value); break;
    case VehicleMileageEntry value: f.Mileage.Add(value); break;
    case FuelClaim value: f.Claims.Add(value); break;
    case ApprovalRequest value: f.Requests.Add(value); break;
    case ApprovalDecision value: f.Decisions.Add(value); break;
}
// SQL supplies these in a live run. The service adapter supplies versions only here.
await (Task)((object)f).GetType().GetMethod("Save", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!.Invoke(f, null)!;
VehicleFuelService service = f.Service;
AuthRequestContext context = f.Context;
void Actor(Guid account, Guid person, string role) {
    AuthenticatedAccount current = f.Actor;
    f.Actor = current with { AccountId = account, EmployeeId = person };
    f.Role = role;
    f.Clock.Now = ((DateTimeOffset)f.Clock.Now).AddSeconds(1);
}
async Task Denied(Func<Task> action, string code) {
    try { await action(); } catch (AuthenticationFailure e) when (e.Code == code) { checks++; Console.WriteLine("PASS denied " + code); return; }
    throw new Exception("Expected denial " + code);
}
var retained = JsonSerializer.Serialize(new { Trip = trip.Id, trip.OdometerEndKm, fuel.Id, fuel.ClaimedAmount,
    fuel.CalculationPrivateReference, Mileage = ((VehicleMileageEntry)rows[6]).CalculationPrivateReference });
var submitted = new List<(VehicleServiceKind Kind, VehicleServiceRecordResponse Response, string Key)>();
async Task Maintenance(int day, VehicleServiceKind kind) {
    var key = $"TEST-HR-V2-{day}-{kind}";
    Actor(hr, employee, "HR");
    var request = new SubmitServiceRecordRequest((Guid)f.Vehicle.Id, kind, new(2026, 9, day), 1200, 200,
        "TEST-HR-V2-WORKSHOP", key, kind == VehicleServiceKind.Tuning ? 50 : 75, "PKR");
    var value = await service.SubmitServiceRecordAsync(request, key, context, default);
    var replay = await service.SubmitServiceRecordAsync(request, key, context, default);
    Check(value.Id == replay.Id, "service submission exact retry " + key);
    Actor(admin, adminEmployee, "ADMIN");
    await service.ApproveServiceRecordAsync(value.Id, key + "-approve", context, default);
    await service.ApproveServiceRecordAsync(value.Id, key + "-approve", context, default);
    submitted.Add((kind, value, key));
    Check(((IEnumerable<VehicleServiceRecord>)f.Records).Count(r => r.Id == value.Id &&
        r.ApprovalRequest!.State == ApprovalState.Approved) == 1, "maintenance approved once " + key);
}
foreach (var day in new[] { 17, 18 }) foreach (var kind in new[] { VehicleServiceKind.Tuning, VehicleServiceKind.EngineOverhaul })
    await Maintenance(day, kind);
Actor(admin, adminEmployee, "ADMIN");
var preview = await service.TripAttributionCorrectionContextAsync(trip.Id, context, default);
var correction = new SubmitVehicleTripAttributionCorrectionRequest(preview.TripVersion, preview.LatestRequestId, preview.SourceFingerprint,
    [new(new(2026, 9, 15), trip.StartedAt, new(2026, 9, 15, 19, 0, 0, TimeSpan.Zero), 1000, 1100, 100, ids[1], null, "TEST V2 first segment"),
     new(new(2026, 9, 16), new(2026, 9, 15, 19, 0, 0, TimeSpan.Zero), trip.EndedAt, 1100, 1200, 100, ids[2], null, "TEST V2 second segment")],
    "TEST V2 source correction", "Explicit synthetic daily route split");
var proposal = await service.SubmitTripAttributionCorrectionAsync(trip.Id, correction, "TEST-V2-correction", context, default);
var correctionReview = new ReviewVehicleAdjustmentRequest(proposal.Version, ApprovalState.Approved, "TEST V2 independent evidence", "Independent review");
await Denied(() => service.ReviewTripAttributionCorrectionAsync(trip.Id, proposal.RequestId, correctionReview, "TEST-V2-self-review", context, default), "vehicle_self_decision_forbidden");
Actor(superadmin, superEmployee, "SUPERADMIN");
await service.ReviewTripAttributionCorrectionAsync(trip.Id, proposal.RequestId, correctionReview, "TEST-V2-correction-review", context, default);
Actor(admin, adminEmployee, "ADMIN");
var comparison = await service.FuelReconciliationContextAsync(fuel.Id, context, default);
Check(comparison.OriginalAmount == 1000 && comparison.CorrectedAmount == 1500 && comparison.DifferenceAmount == 500,
    "actual dated fuel comparison 1000 -> 1500, difference 500");
var reconcile = new SubmitFuelReconciliationRequest(comparison.LatestRequestId, comparison.SourceFingerprint,
    "TEST V2 reconciliation evidence", "Synthetic source comparison only");
var record = await service.SubmitFuelReconciliationAsync(fuel.Id, reconcile, "TEST-V2-reconcile", context, default);
var review = new ReviewVehicleAdjustmentRequest(record.Version, ApprovalState.Approved, "TEST V2 reviewer proof", "Independent comparison review");
await Denied(() => service.ReviewFuelReconciliationAsync(fuel.Id, record.RequestId, review, "self-reconcile", context, default), "vehicle_self_decision_forbidden");
Actor(superadmin, superEmployee, "SUPERADMIN");
var approved = await service.ReviewFuelReconciliationAsync(fuel.Id, record.RequestId, review, "TEST-V2-reconcile-review", context, default);
Check(approved.State == "Approved" && approved.SettlementState == "NotRecorded", "HR comparison approval does not post or pay");
foreach (var day in new[] { 19, 20 }) foreach (var kind in new[] { VehicleServiceKind.Tuning, VehicleServiceKind.EngineOverhaul })
    await Maintenance(day, kind);
Actor(admin, adminEmployee, "ADMIN");
foreach (var item in submitted)
    await service.ApproveServiceRecordAsync(item.Response.Id, item.Key + "-approve", context, default);
Check((int)f.Records.Count == 8 && ((IEnumerable<ApprovalDecision>)f.Decisions).Count() == 12,
    "eight maintenance services, two correction decisions, two retained baseline decisions");
Check(JsonSerializer.Serialize(new { Trip = trip.Id, trip.OdometerEndKm, fuel.Id, fuel.ClaimedAmount,
    fuel.CalculationPrivateReference, Mileage = ((VehicleMileageEntry)rows[6]).CalculationPrivateReference }) == retained,
    "source trip/fuel/mileage remain immutable after entire maintenance chain");
Check(noConnection.Attempts == 0 && noCommand.Attempts == 0, "zero SQL opens/commands; this is not SQL acceptance");
Console.WriteLine($"V2 corpus and service checks passed: {checks}. No database or private-file writes.");

sealed class NoSqlConnection : DbConnectionInterceptor {
    public int Attempts;
    public override InterceptionResult ConnectionOpening(DbConnection c, ConnectionEventData e, InterceptionResult r) { Attempts++; throw new Exception("SQL forbidden"); }
    public override ValueTask<InterceptionResult> ConnectionOpeningAsync(DbConnection c, ConnectionEventData e, InterceptionResult r, CancellationToken t = default) { Attempts++; throw new Exception("SQL forbidden"); }
}
sealed class NoSqlCommand : DbCommandInterceptor {
    public int Attempts;
    public override InterceptionResult<DbDataReader> ReaderExecuting(DbCommand c, CommandEventData e, InterceptionResult<DbDataReader> r) { Attempts++; throw new Exception("SQL forbidden"); }
    public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand c, CommandEventData e, InterceptionResult<DbDataReader> r, CancellationToken t = default) { Attempts++; throw new Exception("SQL forbidden"); }
}
