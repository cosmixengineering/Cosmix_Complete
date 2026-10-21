using Cosmix.Application.HR.VehicleFuel;
using Cosmix.Application.Attendance.Common;
using Cosmix.Application.Attendance.Rosters;
using Cosmix.Domain.Common.Approvals;
using Cosmix.Domain.HR.Policies;
using Cosmix.Domain.HR.VehicleFuel;
using Cosmix.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;

namespace Cosmix.Infrastructure.Repositories.HR;

public sealed class VehicleFuelRepository(CosmixDbContext db) : IVehicleFuelRepository, IVehicleFuelMileageWriter
{
    #region Vehicles

    public Task<Vehicle?> FindVehicleAsync(Guid id, CancellationToken ct) =>
        db.Vehicles.FirstOrDefaultAsync(v => v.Id == id, ct);

    public Task<Vehicle?> FindVehicleByRegistrationAsync(string registrationNumber, CancellationToken ct) =>
        db.Vehicles.FirstOrDefaultAsync(v => v.RegistrationNumber == registrationNumber.ToUpper(), ct);

    public async Task<IReadOnlyList<Vehicle>> ListVehiclesAsync(bool activeOnly, CancellationToken ct)
    {
        var query = db.Vehicles.AsNoTracking().AsQueryable();
        if (activeOnly)
            query = query.Where(v => v.IsActive);
        return await query.OrderBy(v => v.RegistrationNumber).ToArrayAsync(ct);
    }

    public void AddVehicle(Vehicle vehicle) => db.Vehicles.Add(vehicle);

    #endregion

    #region Custody

    public Task<VehicleCustody?> FindCustodyAsync(Guid id, CancellationToken ct) =>
        db.VehicleCustodies
            .Include(c => c.Vehicle)
            .Include(c => c.Employee)
            .FirstOrDefaultAsync(c => c.Id == id, ct);

    public Task<VehicleCustody?> FindActiveCustodyForVehicleAsync(Guid vehicleId, CancellationToken ct) =>
        db.VehicleCustodies
            .Include(c => c.Vehicle)
            .Include(c => c.Employee)
            .FirstOrDefaultAsync(c => c.VehicleId == vehicleId && c.ReturnedAt == null, ct);

    public Task<VehicleCustody?> FindActiveCustodyForEmployeeAsync(Guid employeeId, CancellationToken ct) =>
        db.VehicleCustodies
            .Include(c => c.Vehicle)
            .Include(c => c.Employee)
            .FirstOrDefaultAsync(c => c.EmployeeId == employeeId && c.ReturnedAt == null, ct);

    public async Task<IReadOnlyList<VehicleCustody>> ListCustodyHistoryAsync(Guid? vehicleId, Guid? employeeId, CancellationToken ct)
    {
        var query = db.VehicleCustodies
            .AsNoTracking()
            .Include(c => c.Vehicle)
            .Include(c => c.Employee)
            .AsQueryable();

        if (vehicleId.HasValue)
            query = query.Where(c => c.VehicleId == vehicleId.Value);

        if (employeeId.HasValue)
            query = query.Where(c => c.EmployeeId == employeeId.Value);

        return await query.OrderByDescending(c => c.IssuedAt).ToArrayAsync(ct);
    }

    public void AddCustody(VehicleCustody custody) => db.VehicleCustodies.Add(custody);

    public Task<bool> HasCustodyPeriodOverlapAsync(Guid vehicleId, Guid employeeId, DateTimeOffset issuedAt, CancellationToken ct) =>
        db.VehicleCustodies.AnyAsync(c => (c.VehicleId == vehicleId || c.EmployeeId == employeeId) &&
            (c.ReturnedAt == null || c.ReturnedAt > issuedAt), ct);

    public async Task<bool> HasCustodyReturnConflictAsync(Guid custodyId, DateTimeOffset returnedAt,
        decimal returnOdometerKm, CancellationToken ct)
    {
        var custody = await db.VehicleCustodies.AsNoTracking().SingleOrDefaultAsync(c => c.Id == custodyId, ct)
            ?? throw AttendanceOperation.Missing("custody_not_found");
        if (await db.VehicleTrips.AnyAsync(t => t.VehicleId == custody.VehicleId && t.EmployeeId == custody.EmployeeId &&
            t.ApprovalRequest != null && (t.ApprovalRequest.State == ApprovalState.Pending || t.ApprovalRequest.State == ApprovalState.Approved) &&
            t.StartedAt >= custody.IssuedAt && (custody.ReturnedAt == null || t.EndedAt <= custody.ReturnedAt) &&
            (t.EndedAt > returnedAt || t.OdometerEndKm > returnOdometerKm), ct))
            return true;

        // Match the service workflow's local-day overlap test; a date-only record does not
        // establish an exact service instant on a day when custody is returned.
        var zone = TimeZoneInfo.FindSystemTimeZoneById(RosterSchedule.TimeZoneId);
        var issueDate = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(custody.IssuedAt, zone).DateTime);
        var newReturn = TimeZoneInfo.ConvertTime(returnedAt, zone);
        var newReturnDate = DateOnly.FromDateTime(newReturn.DateTime);
        var newReturnAtMidnight = newReturn.TimeOfDay == TimeSpan.Zero;
        var oldReturn = custody.ReturnedAt is DateTimeOffset end ? TimeZoneInfo.ConvertTime(end, zone) : (DateTimeOffset?)null;
        var oldReturnDate = oldReturn.HasValue ? DateOnly.FromDateTime(oldReturn.Value.DateTime) : (DateOnly?)null;
        var oldReturnAtMidnight = oldReturn.HasValue && oldReturn.Value.TimeOfDay == TimeSpan.Zero;
        return await db.VehicleServiceRecords.AnyAsync(r => r.VehicleId == custody.VehicleId && r.EmployeeId == custody.EmployeeId &&
            r.ApprovalRequest != null && (r.ApprovalRequest.State == ApprovalState.Pending || r.ApprovalRequest.State == ApprovalState.Approved) &&
            r.ServiceDate >= issueDate && (oldReturnDate == null || r.ServiceDate < oldReturnDate ||
                r.ServiceDate == oldReturnDate && !oldReturnAtMidnight) &&
            (r.ServiceDate > newReturnDate || r.ServiceDate == newReturnDate && newReturnAtMidnight ||
                r.OdometerKm > returnOdometerKm), ct);
    }

    public async Task<IReadOnlyList<VehicleCustody>> FindCustodiesOverlappingAsync(
        Guid vehicleId, DateTimeOffset from, DateTimeOffset to, CancellationToken ct) =>
        await db.VehicleCustodies.AsNoTracking().Where(c => c.VehicleId == vehicleId &&
            c.IssuedAt < to && (c.ReturnedAt == null || c.ReturnedAt > from))
            .OrderBy(c => c.IssuedAt).ThenBy(c => c.Id).Take(3).ToArrayAsync(ct);

    #endregion

    #region Commute Distance

    public async Task<EmployeeCommuteDistance?> FindActiveCommuteDistanceAsync(Guid employeeId, DateOnly date, CancellationToken ct)
    {
        var rows = await db.EmployeeCommuteDistances
            .AsNoTracking()
            .Where(c => c.EmployeeId == employeeId && c.EffectiveFrom <= date && (c.EffectiveTo == null || c.EffectiveTo > date))
            .Take(2).ToArrayAsync(ct);
        if (rows.Length > 1) throw AttendanceOperation.Conflict("vehicle_commute_history_ambiguous");
        return rows.SingleOrDefault();
    }

    public Task<bool> HasCommutePeriodOverlapAsync(Guid employeeId, DateOnly effectiveFrom, DateOnly? effectiveTo, CancellationToken ct) =>
        db.EmployeeCommuteDistances.AnyAsync(c => c.EmployeeId == employeeId &&
            (effectiveTo == null || c.EffectiveFrom < effectiveTo) &&
            (c.EffectiveTo == null || c.EffectiveTo > effectiveFrom), ct);

    public void AddCommuteDistance(EmployeeCommuteDistance distance) => db.EmployeeCommuteDistances.Add(distance);

    #endregion

    #region Trips

    public Task<bool> HasTripOverlapAsync(Guid vehicleId, DateTimeOffset startedAt, DateTimeOffset endedAt,
        decimal odometerStartKm, decimal odometerEndKm, Guid? excludeTripId, CancellationToken ct) =>
        db.VehicleTrips.AnyAsync(t => t.VehicleId == vehicleId && (excludeTripId == null || t.Id != excludeTripId) &&
            t.ApprovalRequest != null && (t.ApprovalRequest.State == ApprovalState.Pending || t.ApprovalRequest.State == ApprovalState.Approved) &&
            (t.StartedAt < endedAt && t.EndedAt > startedAt ||
             odometerEndKm > odometerStartKm && t.OdometerEndKm > t.OdometerStartKm &&
             t.OdometerStartKm < odometerEndKm && t.OdometerEndKm > odometerStartKm), ct);

    public Task<VehicleTrip?> FindTripAsync(Guid id, CancellationToken ct) =>
        db.VehicleTrips
            .Include(t => t.Vehicle)
            .Include(t => t.Employee)
            .Include(t => t.ApprovalRequest)
            .FirstOrDefaultAsync(t => t.Id == id, ct);

    public async Task<(IReadOnlyList<VehicleTrip> Items, int TotalCount)> PageTripsAsync(
        Guid? employeeId,
        Guid? vehicleId,
        ApprovalState? state,
        int skip,
        int limit,
        CancellationToken ct)
    {
        var query = db.VehicleTrips
            .AsNoTracking()
            .Include(t => t.Vehicle)
            .Include(t => t.Employee)
            .Include(t => t.ApprovalRequest)
            .AsQueryable();

        if (employeeId.HasValue)
            query = query.Where(t => t.EmployeeId == employeeId.Value);

        if (vehicleId.HasValue)
            query = query.Where(t => t.VehicleId == vehicleId.Value);

        if (state.HasValue)
            query = query.Where(t => t.ApprovalRequest != null && t.ApprovalRequest.State == state.Value);

        var total = await query.CountAsync(ct);
        var items = await query.OrderByDescending(t => t.StartedAt).Skip(skip).Take(limit).ToArrayAsync(ct);
        return (items, total);
    }

    public void AddTrip(VehicleTrip trip, ApprovalRequest approval)
    {
        db.ApprovalRequests.Add(approval);
        db.VehicleTrips.Add(trip);
    }

    #endregion

    #region Fuel Claims

    public Task<FuelClaim?> FindFuelClaimAsync(Guid id, CancellationToken ct) =>
        db.FuelClaims
            .Include(c => c.VehicleTrip)
            .Include(c => c.Employee)
            .Include(c => c.ApprovalRequest)
            .FirstOrDefaultAsync(c => c.Id == id, ct);

    public Task<bool> HasClaimForTripAsync(Guid tripId, CancellationToken ct) =>
        db.FuelClaims.AnyAsync(c => c.VehicleTripId == tripId, ct);

    public async Task<(IReadOnlyList<FuelClaim> Items, int TotalCount)> PageFuelClaimsAsync(
        Guid? employeeId,
        ApprovalState? state,
        int skip,
        int limit,
        CancellationToken ct)
    {
        var query = db.FuelClaims
            .AsNoTracking()
            .Include(c => c.VehicleTrip)
            .Include(c => c.Employee)
            .Include(c => c.ApprovalRequest)
            .AsQueryable();

        if (employeeId.HasValue)
            query = query.Where(c => c.EmployeeId == employeeId.Value);

        if (state.HasValue)
            query = query.Where(c => c.ApprovalRequest != null && c.ApprovalRequest.State == state.Value);

        var total = await query.CountAsync(ct);
        var items = await query.OrderByDescending(c => c.CreatedAt).Skip(skip).Take(limit).ToArrayAsync(ct);
        return (items, total);
    }

    public void AddFuelClaim(FuelClaim claim, ApprovalRequest approval)
    {
        db.ApprovalRequests.Add(approval);
        db.FuelClaims.Add(claim);
    }

    #endregion

    #region Maintenance / Service Records

    public Task<VehicleServiceRecord?> FindServiceRecordAsync(Guid id, CancellationToken ct) =>
        db.VehicleServiceRecords
            .Include(r => r.Vehicle)
            .Include(r => r.Employee)
            .Include(r => r.ApprovalRequest)
            .FirstOrDefaultAsync(r => r.Id == id, ct);

    internal IQueryable<VehicleMileageEntry> ApprovedMileageQuery() =>
        db.VehicleMileageEntries.AsNoTracking().Where(e =>
            e.ApprovalRequest.State == ApprovalState.Approved &&
            e.ApprovalRequest.RequestType == VehicleFuelService.TripRequestType &&
            e.ApprovalRequest.SubjectId == e.VehicleTripId &&
            e.VehicleTrip.ApprovalRequestId == e.ApprovalRequestId &&
            e.VehicleTrip.VehicleId == e.VehicleId && e.VehicleTrip.EmployeeId == e.EmployeeId &&
            db.ApprovalDecisions.Any(d => d.ApprovalRequestId == e.ApprovalRequestId &&
                d.Decision == ApprovalState.Approved));

    public async Task<VehicleMileageEntry?> FindApprovedMileageForTripAsync(Guid tripId, CancellationToken ct)
    {
        var rows = await ApprovedMileageQuery().Where(e => e.VehicleTripId == tripId).Take(2).ToArrayAsync(ct);
        if (rows.Length > 1) throw AttendanceOperation.Conflict("approved_trip_mileage_ambiguous");
        return rows.SingleOrDefault();
    }

    public async Task<decimal> GetAccumulatedOutstationMileageAsync(Guid vehicleId, DateOnly throughDate, CancellationToken ct)
    {
        return await ApprovedMileageQuery().Where(e => e.VehicleId == vehicleId && e.BusinessDate <= throughDate)
            .SumAsync(e => (decimal?)e.OutstationKm, ct) ?? 0m;
    }

    public void AddMileageEntry(VehicleMileageEntry entry) => db.VehicleMileageEntries.Add(entry);

    public async Task<IReadOnlyList<VehicleMileageEntry>> ListApprovedMileageAsync(Guid vehicleId, DateOnly throughDate, CancellationToken ct) =>
        await ApprovedMileageQuery().Where(e => e.VehicleId == vehicleId && e.BusinessDate <= throughDate)
            .OrderBy(e => e.BusinessDate).ThenBy(e => e.Id).Take(10001).ToArrayAsync(ct);

    public async Task<VehicleServiceRecord?> FindLatestApprovedServiceAsync(Guid vehicleId, VehicleServiceKind kind, CancellationToken ct)
    {
        var rows = await db.VehicleServiceRecords.AsNoTracking().Include(r => r.ApprovalRequest)
            .Where(r => r.VehicleId == vehicleId && r.ServiceKind == kind && r.ApprovalRequest != null &&
                r.ApprovalRequest.State == ApprovalState.Approved && r.ApprovalRequest.SubjectId == r.Id &&
                r.ApprovalRequest.RequestType == VehicleFuelService.ServiceRequestType &&
                db.ApprovalDecisions.Any(d => d.ApprovalRequestId == r.ApprovalRequestId && d.Decision == ApprovalState.Approved))
            .OrderByDescending(r => r.ApprovalRequest!.ClosedAt).ThenByDescending(r => r.Id).Take(2).ToArrayAsync(ct);
        // A GUID tie-breaker cannot establish which retained cycle consumed mileage last.
        if (rows.Length > 0 && rows[0].ApprovalRequest!.ClosedAt is null || rows.Length == 2 &&
            rows[0].ApprovalRequest!.ClosedAt == rows[1].ApprovalRequest!.ClosedAt)
            throw AttendanceOperation.Conflict("vehicle_service_cycle_order_ambiguous");
        return rows.FirstOrDefault();
    }

    public Task<bool> HasServiceEvidenceAsync(Guid vehicleId, string evidenceReference, CancellationToken ct) =>
        db.VehicleServiceRecords.AnyAsync(r => r.VehicleId == vehicleId && r.EvidencePrivateReference == evidenceReference &&
            r.ApprovalRequest != null && (r.ApprovalRequest.State == ApprovalState.Pending ||
                r.ApprovalRequest.State == ApprovalState.Approved || r.ApprovalRequest.State == ApprovalState.Returned), ct);

    public async Task<(IReadOnlyList<VehicleServiceRecord> Items, int TotalCount)> PageServiceRecordsAsync(
        Guid? vehicleId,
        Guid? employeeId,
        ApprovalState? state,
        int skip,
        int limit,
        CancellationToken ct)
    {
        var query = db.VehicleServiceRecords
            .AsNoTracking()
            .Include(r => r.Vehicle)
            .Include(r => r.Employee)
            .Include(r => r.ApprovalRequest)
            .AsQueryable();

        if (vehicleId.HasValue)
            query = query.Where(r => r.VehicleId == vehicleId.Value);

        if (employeeId.HasValue)
            query = query.Where(r => r.EmployeeId == employeeId.Value);

        if (state.HasValue)
            query = query.Where(r => r.ApprovalRequest != null && r.ApprovalRequest.State == state.Value);

        var total = await query.CountAsync(ct);
        var items = await query.OrderByDescending(r => r.ServiceDate).Skip(skip).Take(limit).ToArrayAsync(ct);
        return (items, total);
    }

    public void AddServiceRecord(VehicleServiceRecord record, ApprovalRequest approval)
    {
        db.ApprovalRequests.Add(approval);
        db.VehicleServiceRecords.Add(record);
    }

    #endregion

    #region Incidents

    public Task<VehicleIncident?> FindIncidentAsync(Guid id, CancellationToken ct) =>
        db.VehicleIncidents
            .Include(i => i.VehicleCustody)
                .ThenInclude(c => c.Vehicle)
            .Include(i => i.VehicleCustody)
                .ThenInclude(c => c.Employee)
            .Include(i => i.ApprovalRequest)
            .FirstOrDefaultAsync(i => i.Id == id, ct);

    public async Task<(IReadOnlyList<VehicleIncident> Items, int TotalCount)> PageOwnIncidentsAsync(
        Guid employeeId, int skip, int limit, CancellationToken ct)
    {
        var query = db.VehicleIncidents.AsNoTracking().Include(x => x.VehicleCustody).ThenInclude(x => x.Vehicle)
            .Include(x => x.ApprovalRequest).Where(x => x.VehicleCustody.EmployeeId == employeeId);
        var count = await query.CountAsync(ct);
        var rows = await query.OrderByDescending(x => x.OccurredAt).ThenBy(x => x.Id).Skip(skip).Take(limit).ToArrayAsync(ct);
        return (rows, count);
    }

    public async Task<(IReadOnlyList<VehicleIncident> Items, int TotalCount)> PageIncidentsAsync(
        Guid? custodyId,
        ApprovalState? state,
        int skip,
        int limit,
        CancellationToken ct)
    {
        var query = db.VehicleIncidents
            .AsNoTracking()
            .Include(i => i.VehicleCustody)
                .ThenInclude(c => c.Vehicle)
            .Include(i => i.VehicleCustody)
                .ThenInclude(c => c.Employee)
            .Include(i => i.ApprovalRequest)
            .AsQueryable();

        if (custodyId.HasValue)
            query = query.Where(i => i.VehicleCustodyId == custodyId.Value);

        if (state.HasValue)
            query = query.Where(i => i.ApprovalRequest != null && i.ApprovalRequest.State == state.Value);

        var total = await query.CountAsync(ct);
        var items = await query.OrderByDescending(i => i.OccurredAt).ThenByDescending(i => i.Id).Skip(skip).Take(limit).ToArrayAsync(ct);
        return (items, total);
    }

    public void AddIncident(VehicleIncident incident, ApprovalRequest approval)
    {
        db.ApprovalRequests.Add(approval);
        db.VehicleIncidents.Add(incident);
    }

    public Task<ApprovalDecision?> FindIncidentDecisionAsync(Guid approvalRequestId, CancellationToken ct) =>
        db.ApprovalDecisions.AsNoTracking().SingleOrDefaultAsync(d => d.ApprovalRequestId == approvalRequestId, ct);

    #endregion

    #region Policy Resolution

    internal IQueryable<PolicyVersion> AuthorizedPolicyQuery() =>
        db.PolicyVersions.AsNoTracking().Where(v => v.Policy.Category == PolicyCategory.VehicleFuel &&
            (v.Authority == PolicyAuthority.DirectHr && v.ApprovalRequestId == null ||
             v.Authority == PolicyAuthority.ApprovedWorkflow && v.ApprovalRequest != null &&
             v.ApprovalRequest.State == ApprovalState.Approved && v.ApprovalRequest.SubjectId == v.PolicyId &&
             db.ApprovalDecisions.Any(d => d.ApprovalRequestId == v.ApprovalRequestId && d.Decision == ApprovalState.Approved)));

    public async Task<PolicyVersion?> GetActiveVehicleFuelPolicyVersionAsync(DateOnly date, CancellationToken ct)
    {
        var rows = await AuthorizedPolicyQuery().Where(v => v.Policy.IsActive).OrderBy(v => v.EffectiveFrom).ThenBy(v => v.Id).Take(1001).ToArrayAsync(ct);
        return VehiclePolicySuccession.SelectActive(rows, date);
    }

    public Task<PolicyVersion?> FindVehicleFuelPolicyVersionAsync(Guid id, CancellationToken ct) =>
        AuthorizedPolicyQuery().SingleOrDefaultAsync(v => v.Id == id, ct);

    public async Task<(IReadOnlyList<Policy> Items, int TotalCount)> PagePoliciesAsync(int skip, int limit, CancellationToken ct)
    {
        var query = db.Policies.AsNoTracking().Where(p => p.Category == PolicyCategory.VehicleFuel);
        return (await query.OrderBy(p => p.Code).ThenBy(p => p.Id).Skip(skip).Take(limit).ToArrayAsync(ct),
            await query.CountAsync(ct));
    }

    public Task<Policy?> FindPolicyAsync(Guid id, CancellationToken ct) =>
        db.Policies.SingleOrDefaultAsync(p => p.Id == id && p.Category == PolicyCategory.VehicleFuel, ct);

    public Task<bool> PolicyCodeExistsAsync(string code, CancellationToken ct) =>
        db.Policies.AnyAsync(p => p.Code == code, ct);

    public async Task<(IReadOnlyList<PolicyVersion> Items, int TotalCount)> PagePolicyVersionsAsync(Guid policyId, int skip, int limit, CancellationToken ct)
    {
        var query = db.PolicyVersions.AsNoTracking().Where(v => v.PolicyId == policyId && v.Policy.Category == PolicyCategory.VehicleFuel);
        return (await query.OrderByDescending(v => v.VersionNumber).ThenBy(v => v.Id).Skip(skip).Take(limit).ToArrayAsync(ct),
            await query.CountAsync(ct));
    }

    public Task<PolicyVersion?> FindPolicyVersionAsync(Guid policyId, Guid id, CancellationToken ct) =>
        db.PolicyVersions.AsNoTracking().SingleOrDefaultAsync(v => v.Id == id && v.PolicyId == policyId && v.Policy.Category == PolicyCategory.VehicleFuel, ct);

    public async Task<int> LatestPolicyVersionNumberAsync(Guid policyId, CancellationToken ct) =>
        await db.PolicyVersions.Where(v => v.PolicyId == policyId && v.Policy.Category == PolicyCategory.VehicleFuel)
            .Select(v => (int?)v.VersionNumber).MaxAsync(ct) ?? 0;

    // Global selection is the existing vehicle contract. Do not create competing active periods
    // or silently close an old immutable version to make room for a new rate.
    public async Task<bool> HasPolicyPeriodOverlapAsync(DateOnly from, DateOnly to, CancellationToken ct) =>
        VehiclePolicySuccession.HasOverlap(await ListPolicyPeriodVersionsAsync(ct), from, to);

    public async Task<IReadOnlyList<PolicyVersion>> ListPolicyPeriodVersionsAsync(CancellationToken ct) =>
        await db.PolicyVersions.AsNoTracking().Where(v => v.Policy.Category == PolicyCategory.VehicleFuel && v.Policy.IsActive)
            .OrderBy(v => v.EffectiveFrom).ThenBy(v => v.Id).Take(1001).ToArrayAsync(ct);

    public async Task<IReadOnlyList<Guid>> ExistingPolicyEmployeesAsync(IReadOnlyList<Guid> ids, CancellationToken ct) =>
        await db.Employees.AsNoTracking().Where(e => ids.Contains(e.Id)).Select(e => e.Id).ToArrayAsync(ct);

    public async Task<IReadOnlyList<string>> ActivePolicyRoleCodesAsync(IReadOnlyList<string> codes, CancellationToken ct) =>
        await db.Roles.AsNoTracking().Where(r => r.IsActive && codes.Contains(r.Code)).Select(r => r.Code).ToArrayAsync(ct);

    public void AddPolicy(Policy policy) => db.Policies.Add(policy);
    public void AddPolicyVersion(PolicyVersion version) => db.PolicyVersions.Add(version);
    public void ExpectPolicyVersion(Policy policy, byte[] version) => db.Entry(policy).Property(p => p.RowVersion).OriginalValue = version;

    public Task<EmployeeCommuteDistance?> FindCommuteDistanceByIdAsync(Guid id, CancellationToken ct) =>
        db.EmployeeCommuteDistances.AsNoTracking().SingleOrDefaultAsync(c => c.Id == id, ct);

    public async Task<IReadOnlyList<ApprovalRequest>> ListVehicleAdjustmentRequestsAsync(Guid subjectId, string requestType, CancellationToken ct) =>
        await db.ApprovalRequests.AsNoTracking().Where(r => r.SubjectId == subjectId && r.RequestType == requestType)
            .OrderBy(r => r.Stage).ThenBy(r => r.Id).Take(1001).ToArrayAsync(ct);

    public Task<ApprovalRequest?> FindVehicleAdjustmentRequestAsync(Guid id, string requestType, CancellationToken ct) =>
        db.ApprovalRequests.SingleOrDefaultAsync(r => r.Id == id && r.RequestType == requestType, ct);

    public async Task<ApprovalDecision?> FindVehicleAdjustmentDecisionAsync(Guid requestId, CancellationToken ct)
    {
        var rows = await db.ApprovalDecisions.AsNoTracking().Where(d => d.ApprovalRequestId == requestId).Take(2).ToArrayAsync(ct);
        if (rows.Length > 1) throw AttendanceOperation.Conflict("vehicle_adjustment_decision_ambiguous");
        return rows.SingleOrDefault();
    }

    public void AddVehicleAdjustmentRequest(ApprovalRequest request) => db.ApprovalRequests.Add(request);
    public void ExpectVehicleAdjustmentVersion(ApprovalRequest request, byte[] version) =>
        db.Entry(request).Property(r => r.RowVersion).OriginalValue = version;

    #endregion

    public void AddDecision(ApprovalDecision decision) =>
        db.ApprovalDecisions.Add(decision);

    public Task SaveChangesAsync(CancellationToken ct) => db.SaveChangesAsync(ct);
}
