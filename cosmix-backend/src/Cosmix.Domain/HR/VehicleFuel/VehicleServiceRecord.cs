using Cosmix.Domain.Common;
using Cosmix.Domain.HR.Employees;
using Cosmix.Domain.HR.Policies;

namespace Cosmix.Domain.HR.VehicleFuel;

public sealed class VehicleServiceRecord
{
    private VehicleServiceRecord() { }

    public VehicleServiceRecord(Guid vehicleId, Guid employeeId, VehicleServiceKind serviceKind, DateOnly serviceDate, decimal odometerKm, decimal approvedOutstationKm, string authorizedWorkshopReference, string evidencePrivateReference, decimal claimedAmount, string currency, Guid policyVersionId, string eligibilityCalculationPrivateReference, string requestedByUserId, DateTimeOffset createdAt)
    {
        Id = Guid.NewGuid();
        VehicleId = ModelGuard.Id(vehicleId, nameof(vehicleId));
        EmployeeId = ModelGuard.Id(employeeId, nameof(employeeId));
        ServiceKind = Enum.IsDefined(serviceKind) ? serviceKind : throw new ArgumentOutOfRangeException(nameof(serviceKind));
        ServiceDate = serviceDate;
        OdometerKm = HrModelGuard.Distance(odometerKm, nameof(odometerKm));
        ApprovedOutstationKm = HrModelGuard.Distance(approvedOutstationKm, nameof(approvedOutstationKm));
        AuthorizedWorkshopReference = ModelGuard.Required(authorizedWorkshopReference, 200, nameof(authorizedWorkshopReference));
        EvidencePrivateReference = ModelGuard.Required(evidencePrivateReference, 500, nameof(evidencePrivateReference));
        ClaimedAmount = HrModelGuard.Money(claimedAmount, nameof(claimedAmount));
        Currency = HrModelGuard.Currency(currency);
        PolicyVersionId = ModelGuard.Id(policyVersionId, nameof(policyVersionId));
        EligibilityCalculationPrivateReference = ModelGuard.Required(eligibilityCalculationPrivateReference, 500, nameof(eligibilityCalculationPrivateReference));
        RequestedByUserId = ModelGuard.Required(requestedByUserId, 200, nameof(requestedByUserId));
        CreatedAt = ModelGuard.Utc(createdAt);
    }

    public Guid Id { get; private set; }
    public Guid VehicleId { get; private set; }
    public Guid EmployeeId { get; private set; }
    public VehicleServiceKind ServiceKind { get; private set; }
    public DateOnly ServiceDate { get; private set; }
    public decimal OdometerKm { get; private set; }
    public decimal ApprovedOutstationKm { get; private set; }
    public string AuthorizedWorkshopReference { get; private set; } = string.Empty;
    public string EvidencePrivateReference { get; private set; } = string.Empty;
    public decimal ClaimedAmount { get; private set; }
    public string Currency { get; private set; } = string.Empty;
    public Guid PolicyVersionId { get; private set; }
    public string EligibilityCalculationPrivateReference { get; private set; } = string.Empty;
    public string RequestedByUserId { get; private set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; private set; }
    public Vehicle Vehicle { get; private set; } = null!;
    public Employee Employee { get; private set; } = null!;
    public PolicyVersion PolicyVersion { get; private set; } = null!;
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
