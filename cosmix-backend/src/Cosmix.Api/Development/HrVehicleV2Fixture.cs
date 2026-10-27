using System.Data;
using System.Text.Json;
using System.Text.Json.Nodes;
using Cosmix.Application.Attendance.Common;
using Cosmix.Application.Common.Files;
using Cosmix.Application.HR.VehicleFuel;
using Cosmix.Domain.Common.Approvals;
using Cosmix.Domain.HR.Policies;
using Cosmix.Domain.HR.VehicleFuel;
using Cosmix.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Cosmix.Api.Development;

// Explicitly approved synthetic legacy corpus. These are NOT historical HTTP
// approvals, actual journeys, provider verification or payment evidence.
internal static class HrVehicleV2Fixture
{
    internal const string Marker = "TEST-HR-V2-20261026";
    internal const string Workshop = "TEST-HR-V2-WORKSHOP";
    internal static readonly Guid HrAccount = new("8475187f-bf3b-4f9c-a3ed-4bac32253005");
    internal static readonly Guid HrEmployee = new("35d19922-70a8-4c80-8578-e1029360e9fc");
    internal static readonly Guid AdminAccount = new("94a83b62-5b56-4450-9175-2a7a9e3261bf");
    internal static readonly Guid[] Ids = Enumerable.Range(1, 12)
        .Select(i => Guid.Parse($"26102602-{i:0000}-4000-8000-{i:000000000000}")).ToArray();

    internal static async Task<HrVehicleV2SeedResult> SeedAsync(CosmixDbContext db,
        IPrivateFileStore files, Guid vehicleId, Func<CancellationToken, Task> authorize, CancellationToken ct)
    {
        Require(Environment.GetEnvironmentVariable("COSMIX_EXISTING_TESTING_HOST") == "HumaHrVehicle20261026V2");
        Require(OperatingSystem.IsWindows() && db.Database.CurrentTransaction is null && !db.ChangeTracker.Entries().Any());
        var now = DateTimeOffset.UtcNow;
        Require(now >= new DateTimeOffset(2026, 10, 25, 19, 0, 0, TimeSpan.Zero) &&
            now < new DateTimeOffset(2026, 10, 26, 19, 0, 0, TimeSpan.Zero));
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        await authorize(ct);
        // Recheck the physical target inside the same transaction as the corpus.
        await using (var command = db.Database.GetDbConnection().CreateCommand())
        {
            command.Transaction = tx.GetDbTransaction();
            command.CommandText = "SELECT CONVERT(nvarchar(128),SERVERPROPERTY('ServerName')), DB_NAME(), CONVERT(int,SERVERPROPERTY('ProcessID')), ORIGINAL_LOGIN()";
            await using var reader = await command.ExecuteReaderAsync(ct);
            Require(await reader.ReadAsync(ct) && reader.GetString(0) == @"Software-PC1\LOCALDB#8C03C2BC" &&
                reader.GetString(1) == "Cosmix_Testing" && reader.GetInt32(2) == 22112 &&
                reader.GetString(3) == @"COSMIXRND\Huma.Khan");
        }
        var vehicle = await db.Set<Vehicle>().AsNoTracking().SingleOrDefaultAsync(v => v.Id == vehicleId, ct);
        Require(vehicle is { IsActive: true, VehicleType: "Motorcycle", RegistrationNumber: Marker });
        Require(await db.UserAccounts.AsNoTracking().CountAsync(a =>
            a.Id == HrAccount && a.EmployeeId == HrEmployee && a.LoginName == "hr@example.invalid" ||
            a.Id == AdminAccount && a.LoginName == "admin@example.invalid", ct) == 2);
        Require(await db.EmployeeAssignments.AsNoTracking().CountAsync(a => a.EmployeeId == HrEmployee &&
            a.EffectiveFrom <= new DateOnly(2026, 9, 14) && a.EffectiveTo >= new DateOnly(2026, 9, 22), ct) == 1);
        Require(!await db.Policies.AnyAsync(p => p.Category == PolicyCategory.VehicleFuel || p.Code == Marker, ct) &&
            !await db.Set<VehicleTrip>().AnyAsync(ct) && !await db.Set<FuelClaim>().AnyAsync(ct) &&
            !await db.Set<VehicleMileageEntry>().AnyAsync(ct) && !await db.Set<VehicleServiceRecord>().AnyAsync(ct) &&
            !await db.Set<VehicleCustody>().AnyAsync(c => c.VehicleId == vehicleId, ct) &&
            !await db.ApprovalRequests.AnyAsync(a => Ids.Contains(a.Id) || a.SubjectId == Ids[5] || a.SubjectId == Ids[7], ct) &&
            !await db.ApprovalDecisions.AnyAsync(a => Ids.Contains(a.Id), ct));
        var writes = 0;
        var rows = await CreateRowsAsync(db, vehicleId, now, async (bytes, purpose) =>
        {
            Require(++writes <= 5);
            return AttendanceOperation.Reference(await files.WriteAsync(bytes, purpose, ct));
        });
        Require(writes == 5);
        await authorize(ct);
        db.AddRange(rows);
        RequireExactTrackedRows(db, rows);
        var saved = await db.SaveChangesAsync(ct);
        Require(saved == 12 && rows.All(row => db.Entry(row).State == EntityState.Unchanged));
        await tx.CommitAsync(ct);
        return new(vehicleId, Ids[5], Ids[7], [Ids[1], Ids[2], Ids[3]], 12, writes);
    }

    internal static void RequireExactTrackedRows(CosmixDbContext db, object[] rows)
    {
        db.ChangeTracker.DetectChanges();
        var entries = db.ChangeTracker.Entries().ToArray();
        Require(rows.Length == 12 && entries.Length == 12 &&
            entries.All(e => e.State == EntityState.Added && rows.Contains(e.Entity) &&
                Ids.Contains((Guid)e.Property("Id").CurrentValue!)));
    }

    internal static async Task<object[]> CreateRowsAsync(CosmixDbContext db, Guid vehicleId,
        DateTimeOffset now, Func<byte[], string, Task<string>> store)
    {
        Require(vehicleId != Guid.Empty && now.Offset == TimeSpan.Zero && !db.ChangeTracker.Entries().Any());
        T Pin<T>(T entity, int index) where T : class
        {
            db.Entry(entity).Property("Id").CurrentValue = Ids[index];
            return entity;
        }
        Task<string> Write(object value, string purpose) => store(JsonSerializer.SerializeToUtf8Bytes(value), purpose);
        var provenance = await Write(new { Marker, Synthetic = true, ExecutedHistoricalHttp = false,
            RealJourney = false, Payment = false, PreparedAt = now, VehicleId = vehicleId,
            SourceIds = Ids }, "test:vehicle:v2:corpus");
        var policy = Pin(new Policy(Marker, "TEST ONLY synthetic historical vehicle policy", PolicyCategory.VehicleFuel), 0);
        var intervals = new[] { (new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 16), 100m),
            (new DateOnly(2026, 9, 16), new DateOnly(2026, 10, 26), 200m),
            (new DateOnly(2026, 10, 26), new DateOnly(2026, 10, 27), 200m) };
        var versions = intervals.Select((range, index) => Pin(new PolicyVersion(policy.Id, index + 1,
            range.Item1, range.Item2, VehiclePolicyRulesCodec.Schema, Rules(range.Item1, range.Item2, range.Item3),
            provenance, null, AdminAccount.ToString(), now, PolicyAuthority.DirectHr), index + 1)).ToArray();
        var custody = Pin(new VehicleCustody(vehicleId, HrEmployee, new(2026, 9, 14, 0, 0, 0, TimeSpan.Zero),
            1000, provenance, AdminAccount.ToString()), 4);
        custody.RecordReturn(new(2026, 9, 21, 18, 0, 0, TimeSpan.Zero), 1200, provenance, AdminAccount.ToString());
        var trip = Pin(new VehicleTrip(HrEmployee, vehicleId, VehicleJourneyPurpose.SiteToSite,
            new(2026, 9, 15, 18, 0, 0, TimeSpan.Zero), new(2026, 9, 16, 1, 0, 0, TimeSpan.Zero),
            "TEST ONLY origin", "TEST ONLY destination", 1000, 1200, 200, provenance,
            Marker, HrAccount.ToString(), now), 5);
        var tripSnapshot = await Write(new { Marker, Synthetic = true, TripId = trip.Id,
            trip.EmployeeId, trip.VehicleId, trip.Purpose, trip.StartedAt, trip.EndedAt, trip.OdometerStartKm,
            trip.OdometerEndKm, trip.MapDistanceKm, trip.RequestedByUserId, trip.CreatedAt }, $"vehicle:trip:{trip.Id:N}");
        var tripHeader = Pin(new ApprovalRequest(VehicleFuelService.TripRequestType, trip.Id,
            HrAccount.ToString(), tripSnapshot, 1, now), 8);
        trip.Submit(tripHeader);
        var mileageReference = await Write(new { SchemaVersion = 3, TripId = trip.Id, VehicleId = vehicleId,
            EmployeeId = HrEmployee, trip.Purpose, BusinessDate = new DateOnly(2026, 9, 15),
            trip.OdometerStartKm, trip.OdometerEndKm, ApprovedBusinessKm = 200m, ExcludedCommuteKm = 0m,
            OutstationKm = 200m, AccumulatedOutstationKmBefore = 0m, PolicyVersionId = versions[0].Id,
            ApprovalRequestId = tripHeader.Id, ActorAccountId = AdminAccount, RecordedAt = now,
            trip.MapDistanceKm, RouteEvidenceReference = trip.RouteEvidencePrivateReference,
            CommuteDistanceId = (Guid?)null, DistanceBasis = "ApprovedMapDistance",
            StaffEligibility = new VehicleStaffEligibilityRule(HrEmployee, intervals[0].Item1, intervals[0].Item2,
                ["FieldVisits"], Marker) }, $"vehicle:mileage:{trip.Id:N}");
        var tripDecision = Pin(tripHeader.Decide(ApprovalState.Approved, AdminAccount.ToString(), "ADMIN",
            "TEST ONLY synthetic legacy baseline; not an executed approval", now, mileageReference), 10);
        var mileage = Pin(new VehicleMileageEntry(vehicleId, HrEmployee, trip.Id, new(2026, 9, 15),
            200, 0, 200, 0, mileageReference, versions[0].Id, tripHeader.Id, Marker + "-legacy-mileage",
            AdminAccount.ToString(), now), 6);
        var fuelReference = await Write(new { SchemaVersion = 1, TripId = trip.Id, MileageEntryId = mileage.Id,
            CalculationPrivateReference = mileageReference, PolicyVersionId = versions[0].Id,
            EligibleDistanceKm = 200m, ExcludedCommuteKm = 0m, KmPerLiter = 20m, FuelPricePerLiter = 100m,
            FuelLiters = 10m, ClaimedAmount = 1000m, Currency = "PKR" }, $"vehicle:fuel:{trip.Id:N}");
        var fuel = Pin(new FuelClaim(HrEmployee, trip.Id, null, 200, 0, 10, 1000, "PKR", versions[0].Id,
            fuelReference, provenance, HrAccount.ToString(), now), 7);
        var fuelSnapshot = await Write(new { Marker, Synthetic = true, ClaimId = fuel.Id, fuel.EmployeeId,
            fuel.VehicleTripId, fuel.PolicyVersionId, fuel.CalculationPrivateReference, fuel.ClaimedAmount,
            fuel.FuelLiters, fuel.Currency }, $"vehicle:fuel:claim:{fuel.Id:N}");
        var fuelHeader = Pin(new ApprovalRequest(VehicleFuelService.FuelRequestType, fuel.Id,
            HrAccount.ToString(), fuelSnapshot, 1, now), 9);
        fuel.Submit(fuelHeader);
        var fuelDecision = Pin(fuelHeader.Decide(ApprovalState.Approved, AdminAccount.ToString(), "ADMIN",
            "TEST ONLY synthetic legacy baseline; not posted or paid", now, fuelReference), 11);
        object[] rows = [policy, .. versions, custody, trip, mileage, fuel, tripHeader, fuelHeader, tripDecision, fuelDecision];
        Require(rows.Length == 12 && rows.Select((row, i) => (Guid)db.Entry(row).Property("Id").CurrentValue! == Ids[i]).All(x => x));
        Require(!db.ChangeTracker.Entries().Any());
        return rows;
    }

    internal static string Rules(DateOnly from, DateOnly to, decimal price)
    {
        var rules = new VehiclePolicyRules(2, 20, price, 50, 50,
            new(1, [new(HrEmployee, from, to, ["FieldVisits"], Marker)],
                [new(Workshop, from, to, [VehicleServiceKind.Tuning, VehicleServiceKind.EngineOverhaul], Marker)],
                [new(VehicleServiceKind.Tuning, "EveryThreshold", "Fixed", 50, "PKR", ["Motorcycle"]),
                 new(VehicleServiceKind.EngineOverhaul, "EveryThreshold", "Fixed", 75, "PKR", ["Motorcycle"])],
                "Odometer", new(["ADMIN", "SUPERADMIN"], Marker), "PKR"),
            new(2, null, [], null, new("ExplicitDailySegments", "OncePerTripDay", "PerSegmentTwoDecimalsToEven",
                ["ADMIN", "SUPERADMIN"], Marker), null));
        var json = JsonSerializer.SerializeToNode(rules)!.AsObject();
        json["Adjustments"]!["MonthlyPeriods"] = null;
        var result = VehiclePolicyRulesCodec.Validate(JsonSerializer.SerializeToElement(json), from, to);
        Require(result.IsValid && result.CanonicalJson is not null);
        return result.CanonicalJson!;
    }

    private static void Require(bool value)
    {
        if (!value) throw new InvalidOperationException("V2 synthetic legacy corpus guard refused execution.");
    }
}

internal sealed record HrVehicleV2SeedResult(Guid VehicleId, Guid TripId, Guid FuelClaimId,
    IReadOnlyList<Guid> PolicyVersionIds, int SourceRows, int PrivateFiles);
