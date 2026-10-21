using Cosmix.Domain.Common;
using Cosmix.Domain.HR.Employees;
using Cosmix.Domain.HR.Policies;

namespace Cosmix.Domain.HR.VehicleFuel;

public sealed class FuelClaim
{
    private FuelClaim() { }

    public FuelClaim(Guid employeeId, Guid vehicleTripId, Guid? employeeCommuteDistanceId, decimal eligibleDistanceKm, decimal excludedCommuteKm, decimal fuelLiters, decimal claimedAmount, string currency, Guid policyVersionId, string calculationPrivateReference, string? receiptPrivateReference, string requestedByUserId, DateTimeOffset createdAt)
    {
        Id = Guid.NewGuid();
        EmployeeId = ModelGuard.Id(employeeId, nameof(employeeId));
        VehicleTripId = ModelGuard.Id(vehicleTripId, nameof(vehicleTripId));
        EmployeeCommuteDistanceId = employeeCommuteDistanceId.HasValue ? ModelGuard.Id(employeeCommuteDistanceId.Value, nameof(employeeCommuteDistanceId)) : null;
        EligibleDistanceKm = HrModelGuard.Distance(eligibleDistanceKm, nameof(eligibleDistanceKm));
        ExcludedCommuteKm = HrModelGuard.Distance(excludedCommuteKm, nameof(excludedCommuteKm));
        FuelLiters = HrModelGuard.Distance(fuelLiters, nameof(fuelLiters));
        ClaimedAmount = HrModelGuard.Money(claimedAmount, nameof(claimedAmount));
        Currency = HrModelGuard.Currency(currency);
        PolicyVersionId = ModelGuard.Id(policyVersionId, nameof(policyVersionId));
        CalculationPrivateReference = ModelGuard.Required(calculationPrivateReference, 500, nameof(calculationPrivateReference));
        ReceiptPrivateReference = ModelGuard.Optional(receiptPrivateReference, 500, nameof(receiptPrivateReference));
        RequestedByUserId = ModelGuard.Required(requestedByUserId, 200, nameof(requestedByUserId));
        CreatedAt = ModelGuard.Utc(createdAt);
    }

    public Guid Id { get; private set; }
    public Guid EmployeeId { get; private set; }
    public Guid VehicleTripId { get; private set; }
    public Guid? EmployeeCommuteDistanceId { get; private set; }
    public decimal EligibleDistanceKm { get; private set; }
    public decimal ExcludedCommuteKm { get; private set; }
    public decimal FuelLiters { get; private set; }
    public decimal ClaimedAmount { get; private set; }
    public string Currency { get; private set; } = string.Empty;
    public Guid PolicyVersionId { get; private set; }
    public string CalculationPrivateReference { get; private set; } = string.Empty;
    public string? ReceiptPrivateReference { get; private set; }
    public string RequestedByUserId { get; private set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; private set; }
    public Employee Employee { get; private set; } = null!;
    public VehicleTrip VehicleTrip { get; private set; } = null!;
    public EmployeeCommuteDistance? EmployeeCommuteDistance { get; private set; }
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
