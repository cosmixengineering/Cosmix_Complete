using Cosmix.Domain.Common.Approvals;
using Cosmix.Domain.HR.Policies;
using Cosmix.Domain.HR.VehicleFuel;

namespace Cosmix.Application.HR.VehicleFuel;

public interface IVehicleFuelRepository
{
    // Vehicles
    Task<Vehicle?> FindVehicleAsync(Guid id, CancellationToken ct);
    Task<Vehicle?> FindVehicleByRegistrationAsync(string registrationNumber, CancellationToken ct);
    Task<IReadOnlyList<Vehicle>> ListVehiclesAsync(bool activeOnly, CancellationToken ct);
    void AddVehicle(Vehicle vehicle);

    // Custody
    Task<VehicleCustody?> FindCustodyAsync(Guid id, CancellationToken ct);
    Task<VehicleCustody?> FindActiveCustodyForVehicleAsync(Guid vehicleId, CancellationToken ct);
    Task<VehicleCustody?> FindActiveCustodyForEmployeeAsync(Guid employeeId, CancellationToken ct);
    Task<IReadOnlyList<VehicleCustody>> FindCustodiesOverlappingAsync(Guid vehicleId, DateTimeOffset from, DateTimeOffset to, CancellationToken ct);
    Task<IReadOnlyList<VehicleCustody>> ListCustodyHistoryAsync(Guid? vehicleId, Guid? employeeId, CancellationToken ct);
    void AddCustody(VehicleCustody custody);
    Task<bool> HasCustodyPeriodOverlapAsync(Guid vehicleId, Guid employeeId, DateTimeOffset issuedAt, CancellationToken ct);
    Task<bool> HasCustodyReturnConflictAsync(Guid custodyId, DateTimeOffset returnedAt, decimal returnOdometerKm, CancellationToken ct);

    // Commute Distance
    Task<EmployeeCommuteDistance?> FindActiveCommuteDistanceAsync(Guid employeeId, DateOnly date, CancellationToken ct);
    void AddCommuteDistance(EmployeeCommuteDistance distance);
    Task<bool> HasCommutePeriodOverlapAsync(Guid employeeId, DateOnly effectiveFrom, DateOnly? effectiveTo, CancellationToken ct);

    // Trips
    Task<VehicleTrip?> FindTripAsync(Guid id, CancellationToken ct);
    Task<VehicleMileageEntry?> FindApprovedMileageForTripAsync(Guid tripId, CancellationToken ct);
    Task<(IReadOnlyList<VehicleTrip> Items, int TotalCount)> PageTripsAsync(Guid? employeeId, Guid? vehicleId, ApprovalState? state, int skip, int limit, CancellationToken ct);
    void AddTrip(VehicleTrip trip, ApprovalRequest approval);
    Task<bool> HasTripOverlapAsync(Guid vehicleId, DateTimeOffset startedAt, DateTimeOffset endedAt,
        decimal odometerStartKm, decimal odometerEndKm, Guid? excludeTripId, CancellationToken ct);

    // Fuel Claims
    Task<FuelClaim?> FindFuelClaimAsync(Guid id, CancellationToken ct);
    Task<bool> HasClaimForTripAsync(Guid tripId, CancellationToken ct);
    Task<(IReadOnlyList<FuelClaim> Items, int TotalCount)> PageFuelClaimsAsync(Guid? employeeId, ApprovalState? state, int skip, int limit, CancellationToken ct);
    void AddFuelClaim(FuelClaim claim, ApprovalRequest approval);

    // Maintenance / Service Records
    Task<VehicleServiceRecord?> FindServiceRecordAsync(Guid id, CancellationToken ct);
    Task<decimal> GetAccumulatedOutstationMileageAsync(Guid vehicleId, DateOnly throughDate, CancellationToken ct);
    Task<IReadOnlyList<VehicleMileageEntry>> ListApprovedMileageAsync(Guid vehicleId, DateOnly throughDate, CancellationToken ct);
    Task<VehicleServiceRecord?> FindLatestApprovedServiceAsync(Guid vehicleId, VehicleServiceKind kind, CancellationToken ct);
    Task<bool> HasServiceEvidenceAsync(Guid vehicleId, string evidenceReference, CancellationToken ct);
    Task<(IReadOnlyList<VehicleServiceRecord> Items, int TotalCount)> PageServiceRecordsAsync(Guid? vehicleId, Guid? employeeId, ApprovalState? state, int skip, int limit, CancellationToken ct);
    void AddServiceRecord(VehicleServiceRecord record, ApprovalRequest approval);

    // Incidents
    Task<VehicleIncident?> FindIncidentAsync(Guid id, CancellationToken ct);
    Task<(IReadOnlyList<VehicleIncident> Items, int TotalCount)> PageIncidentsAsync(Guid? custodyId, ApprovalState? state, int skip, int limit, CancellationToken ct);
    Task<(IReadOnlyList<VehicleIncident> Items, int TotalCount)> PageOwnIncidentsAsync(Guid employeeId, int skip, int limit, CancellationToken ct);
    void AddIncident(VehicleIncident incident, ApprovalRequest approval);
    Task<ApprovalDecision?> FindIncidentDecisionAsync(Guid approvalRequestId, CancellationToken ct);

    // Policy resolution
    Task<PolicyVersion?> GetActiveVehicleFuelPolicyVersionAsync(DateOnly date, CancellationToken ct);
    Task<PolicyVersion?> FindVehicleFuelPolicyVersionAsync(Guid id, CancellationToken ct);

    // HR publication uses the same immutable policy rows consumed by retained vehicle claims.
    Task<(IReadOnlyList<Policy> Items, int TotalCount)> PagePoliciesAsync(int skip, int limit, CancellationToken ct);
    Task<Policy?> FindPolicyAsync(Guid id, CancellationToken ct);
    Task<bool> PolicyCodeExistsAsync(string code, CancellationToken ct);
    Task<(IReadOnlyList<PolicyVersion> Items, int TotalCount)> PagePolicyVersionsAsync(Guid policyId, int skip, int limit, CancellationToken ct);
    Task<PolicyVersion?> FindPolicyVersionAsync(Guid policyId, Guid id, CancellationToken ct);
    Task<int> LatestPolicyVersionNumberAsync(Guid policyId, CancellationToken ct);
    Task<bool> HasPolicyPeriodOverlapAsync(DateOnly from, DateOnly to, CancellationToken ct);
    Task<IReadOnlyList<PolicyVersion>> ListPolicyPeriodVersionsAsync(CancellationToken ct);
    Task<IReadOnlyList<Guid>> ExistingPolicyEmployeesAsync(IReadOnlyList<Guid> ids, CancellationToken ct);
    Task<IReadOnlyList<string>> ActivePolicyRoleCodesAsync(IReadOnlyList<string> codes, CancellationToken ct);
    void AddPolicy(Policy policy);
    void AddPolicyVersion(PolicyVersion version);
    void ExpectPolicyVersion(Policy policy, byte[] version);

    Task<EmployeeCommuteDistance?> FindCommuteDistanceByIdAsync(Guid id, CancellationToken ct);
    Task<IReadOnlyList<ApprovalRequest>> ListVehicleAdjustmentRequestsAsync(Guid subjectId, string requestType, CancellationToken ct);
    Task<ApprovalRequest?> FindVehicleAdjustmentRequestAsync(Guid id, string requestType, CancellationToken ct);
    Task<ApprovalDecision?> FindVehicleAdjustmentDecisionAsync(Guid requestId, CancellationToken ct);
    void AddVehicleAdjustmentRequest(ApprovalRequest request);
    void ExpectVehicleAdjustmentVersion(ApprovalRequest request, byte[] version);

    void AddDecision(ApprovalDecision decision);

    Task SaveChangesAsync(CancellationToken ct);
}
