using Cosmix.Domain.Common;
using Cosmix.Domain.HR.Employees;

namespace Cosmix.Domain.HR.VehicleFuel;

public sealed class VehicleTrip
{
    private VehicleTrip() { }

    public VehicleTrip(Guid employeeId, Guid vehicleId, VehicleJourneyPurpose purpose, DateTimeOffset startedAt, DateTimeOffset endedAt, string originLabel, string destinationLabel, decimal odometerStartKm, decimal odometerEndKm, decimal mapDistanceKm, string routeEvidencePrivateReference, string workReference, string requestedByUserId, DateTimeOffset createdAt)
    {
        if (endedAt < startedAt || odometerEndKm < odometerStartKm) throw new ArgumentException("Trip end must follow start with a nondecreasing odometer.");
        Id = Guid.NewGuid();
        EmployeeId = ModelGuard.Id(employeeId, nameof(employeeId));
        VehicleId = ModelGuard.Id(vehicleId, nameof(vehicleId));
        Purpose = Enum.IsDefined(purpose) ? purpose : throw new ArgumentOutOfRangeException(nameof(purpose));
        StartedAt = ModelGuard.Utc(startedAt);
        EndedAt = ModelGuard.Utc(endedAt);
        OriginLabel = ModelGuard.Required(originLabel, 200, nameof(originLabel));
        DestinationLabel = ModelGuard.Required(destinationLabel, 200, nameof(destinationLabel));
        OdometerStartKm = HrModelGuard.Distance(odometerStartKm, nameof(odometerStartKm));
        OdometerEndKm = HrModelGuard.Distance(odometerEndKm, nameof(odometerEndKm));
        MapDistanceKm = HrModelGuard.Distance(mapDistanceKm, nameof(mapDistanceKm));
        RouteEvidencePrivateReference = ModelGuard.Required(routeEvidencePrivateReference, 500, nameof(routeEvidencePrivateReference));
        WorkReference = ModelGuard.Required(workReference, 150, nameof(workReference));
        RequestedByUserId = ModelGuard.Required(requestedByUserId, 200, nameof(requestedByUserId));
        CreatedAt = ModelGuard.Utc(createdAt);
    }

    public Guid Id { get; private set; }
    public Guid EmployeeId { get; private set; }
    public Guid VehicleId { get; private set; }
    public VehicleJourneyPurpose Purpose { get; private set; }
    public DateTimeOffset StartedAt { get; private set; }
    public DateTimeOffset EndedAt { get; private set; }
    public string OriginLabel { get; private set; } = string.Empty;
    public string DestinationLabel { get; private set; } = string.Empty;
    public decimal OdometerStartKm { get; private set; }
    public decimal OdometerEndKm { get; private set; }
    public decimal MapDistanceKm { get; private set; }
    public string RouteEvidencePrivateReference { get; private set; } = string.Empty;
    public string WorkReference { get; private set; } = string.Empty;
    public string RequestedByUserId { get; private set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; private set; }
    public Employee Employee { get; private set; } = null!;
    public Vehicle Vehicle { get; private set; } = null!;
    public byte[] RowVersion { get; private set; } = [];

    public Guid? ApprovalRequestId { get; private set; }
    public Cosmix.Domain.Common.Approvals.ApprovalRequest? ApprovalRequest { get; private set; }
    public DateTimeOffset? SubmittedAt { get; private set; }

    public void Submit(Cosmix.Domain.Common.Approvals.ApprovalRequest approval)
    {
        ArgumentNullException.ThrowIfNull(approval);
        if (SubmittedAt.HasValue || approval.SubjectId != Id || approval.RequesterUserId != RequestedByUserId || approval.RequestedAt < CreatedAt || approval.State != Cosmix.Domain.Common.Approvals.ApprovalState.Pending)
            throw new InvalidOperationException("Submission needs a pending approval for this request and its requester.");
        ApprovalRequestId = approval.Id; ApprovalRequest = approval; SubmittedAt = approval.RequestedAt;
    }
}
