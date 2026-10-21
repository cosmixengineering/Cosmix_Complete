using Cosmix.Domain.Common;
using Cosmix.Domain.Common.Approvals;
using Cosmix.Domain.HR.Employees;
using Cosmix.Domain.HR.Policies;

namespace Cosmix.Domain.HR.VehicleFuel;

/// <summary>Approved kilometers and commute/monthly adjustments form mileage evidence for maintenance milestones, without hard-coded rates.</summary>
public sealed class VehicleMileageEntry : IAppendOnlyEntity
{
    private VehicleMileageEntry() { }

    public VehicleMileageEntry(Guid vehicleId, Guid employeeId, Guid vehicleTripId, DateOnly businessDate, decimal approvedBusinessKm, decimal excludedCommuteKm, decimal outstationKm, decimal monthlyAdjustmentKm, string calculationPrivateReference, Guid policyVersionId, Guid approvalRequestId, string idempotencyKey, string recordedByUserId, DateTimeOffset recordedAt)
    {
        if (outstationKm > approvedBusinessKm) throw new ArgumentException("Outstation distance cannot exceed approved business distance.");
        Id = Guid.NewGuid();
        VehicleId = ModelGuard.Id(vehicleId, nameof(vehicleId));
        EmployeeId = ModelGuard.Id(employeeId, nameof(employeeId));
        VehicleTripId = ModelGuard.Id(vehicleTripId, nameof(vehicleTripId));
        BusinessDate = businessDate;
        ApprovedBusinessKm = HrModelGuard.Distance(approvedBusinessKm, nameof(approvedBusinessKm));
        ExcludedCommuteKm = HrModelGuard.Distance(excludedCommuteKm, nameof(excludedCommuteKm));
        OutstationKm = HrModelGuard.Distance(outstationKm, nameof(outstationKm));
        MonthlyAdjustmentKm = HrModelGuard.SignedDistance(monthlyAdjustmentKm, nameof(monthlyAdjustmentKm));
        CalculationPrivateReference = ModelGuard.Required(calculationPrivateReference, 500, nameof(calculationPrivateReference));
        PolicyVersionId = ModelGuard.Id(policyVersionId, nameof(policyVersionId));
        ApprovalRequestId = ModelGuard.Id(approvalRequestId, nameof(approvalRequestId));
        IdempotencyKey = ModelGuard.Required(idempotencyKey, 100, nameof(idempotencyKey));
        RecordedByUserId = ModelGuard.Required(recordedByUserId, 200, nameof(recordedByUserId));
        RecordedAt = ModelGuard.Utc(recordedAt);
    }

    public Guid Id { get; private set; }
    public Guid VehicleId { get; private set; }
    public Guid EmployeeId { get; private set; }
    public Guid VehicleTripId { get; private set; }
    public DateOnly BusinessDate { get; private set; }
    public decimal ApprovedBusinessKm { get; private set; }
    public decimal ExcludedCommuteKm { get; private set; }
    public decimal OutstationKm { get; private set; }
    public decimal MonthlyAdjustmentKm { get; private set; }
    public string CalculationPrivateReference { get; private set; } = string.Empty;
    public Guid PolicyVersionId { get; private set; }
    public Guid ApprovalRequestId { get; private set; }
    public string IdempotencyKey { get; private set; } = string.Empty;
    public string RecordedByUserId { get; private set; } = string.Empty;
    public DateTimeOffset RecordedAt { get; private set; }
    public Vehicle Vehicle { get; private set; } = null!;
    public Employee Employee { get; private set; } = null!;
    public VehicleTrip VehicleTrip { get; private set; } = null!;
    public PolicyVersion PolicyVersion { get; private set; } = null!;
    public ApprovalRequest ApprovalRequest { get; private set; } = null!;
}
