using Cosmix.Domain.Common;

namespace Cosmix.Domain.HR.VehicleFuel;

/// <summary>Accident and liability evidence are recorded for human approval; no automatic salary deduction follows an incident.</summary>
public sealed class VehicleIncident
{
    private VehicleIncident() { }

    public VehicleIncident(Guid vehicleCustodyId, DateTimeOffset occurredAt, string incidentEvidencePrivateReference, string? thirdPartyDetailsEncryptedReference, string? insuranceClaimReference, decimal? proposedEmployeeLiability, string? currency, string requestedByUserId, DateTimeOffset createdAt)
    {
        if (proposedEmployeeLiability.HasValue != (currency != null)) throw new ArgumentException("A liability amount and currency must be supplied together.");
        Id = Guid.NewGuid();
        VehicleCustodyId = ModelGuard.Id(vehicleCustodyId, nameof(vehicleCustodyId));
        OccurredAt = ModelGuard.Utc(occurredAt);
        IncidentEvidencePrivateReference = ModelGuard.Required(incidentEvidencePrivateReference, 500, nameof(incidentEvidencePrivateReference));
        ThirdPartyDetailsEncryptedReference = ModelGuard.Optional(thirdPartyDetailsEncryptedReference, 500, nameof(thirdPartyDetailsEncryptedReference));
        InsuranceClaimReference = ModelGuard.Optional(insuranceClaimReference, 200, nameof(insuranceClaimReference));
        ProposedEmployeeLiability = proposedEmployeeLiability.HasValue ? HrModelGuard.Money(proposedEmployeeLiability.Value, nameof(proposedEmployeeLiability)) : null;
        Currency = currency is null ? null : HrModelGuard.Currency(currency);
        RequestedByUserId = ModelGuard.Required(requestedByUserId, 200, nameof(requestedByUserId));
        CreatedAt = ModelGuard.Utc(createdAt);
    }

    public Guid Id { get; private set; }
    public Guid VehicleCustodyId { get; private set; }
    public DateTimeOffset OccurredAt { get; private set; }
    public string IncidentEvidencePrivateReference { get; private set; } = string.Empty;
    public string? ThirdPartyDetailsEncryptedReference { get; private set; }
    public string? InsuranceClaimReference { get; private set; }
    public decimal? ProposedEmployeeLiability { get; private set; }
    public string? Currency { get; private set; }
    public string RequestedByUserId { get; private set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; private set; }
    public VehicleCustody VehicleCustody { get; private set; } = null!;
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
