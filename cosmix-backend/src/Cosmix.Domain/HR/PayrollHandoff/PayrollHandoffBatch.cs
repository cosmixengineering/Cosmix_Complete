using System.ComponentModel.DataAnnotations.Schema;
using Cosmix.Domain.Common;

namespace Cosmix.Domain.HR.PayrollHandoff;

/// <summary>An approved handoff snapshot. Application use cases enforce Accounts/management boundaries, frozen lines, period locks and post-payroll adjustments.</summary>
public sealed class PayrollHandoffBatch
{
    private PayrollHandoffBatch() { }

    public PayrollHandoffBatch(string batchReference, DateOnly periodStart, DateOnly periodEnd, string currency, string preparedByUserId, DateTimeOffset preparedAt)
    {
        if (periodEnd < periodStart) throw new ArgumentException("Payroll period end cannot precede its start.");
        Id = Guid.NewGuid();
        BatchReference = ModelGuard.Required(batchReference, 100, nameof(batchReference));
        PeriodStart = periodStart;
        PeriodEnd = periodEnd;
        Currency = HrModelGuard.Currency(currency);
        PreparedByUserId = ModelGuard.Required(preparedByUserId, 200, nameof(preparedByUserId));
        PreparedAt = ModelGuard.Utc(preparedAt);
    }

    public Guid Id { get; private set; }
    public string BatchReference { get; private set; } = string.Empty;
    public DateOnly PeriodStart { get; private set; }
    public DateOnly PeriodEnd { get; private set; }
    public string Currency { get; private set; } = string.Empty;
    public string PreparedByUserId { get; private set; } = string.Empty;
    public DateTimeOffset PreparedAt { get; private set; }
    public int LinesRevision { get; private set; }

    [NotMapped]
    public string CalculationVersion { get; private set; } = "adjustments-v1";
    [NotMapped]
    public bool SalaryComplete => CalculationVersion == "salary-v2" && !string.IsNullOrWhiteSpace(FrozenFingerprint);
    [NotMapped]
    public string? PreviewFingerprint { get; private set; }
    [NotMapped]
    public string? FrozenFingerprint { get; private set; }
    [NotMapped]
    public string? CalculationPrivateReference { get; private set; }
    [NotMapped]
    public string ReviewState { get; private set; } = "prepared";
    [NotMapped]
    public Guid? HrApprovalRequestId { get; private set; }
    [NotMapped]
    public string? HrApprovedByUserId { get; private set; }
    [NotMapped]
    public DateTimeOffset? HrApprovedAt { get; private set; }

    public byte[] RowVersion { get; private set; } = [];

    public DateTimeOffset? LockedAt { get; private set; }
    public string? LockedByUserId { get; private set; }
    public Guid? ApprovalRequestId { get; private set; }
    public Cosmix.Domain.Common.Approvals.ApprovalRequest? ApprovalRequest { get; private set; }
    public ICollection<PayrollHandoffLine> Lines { get; private set; } = new List<PayrollHandoffLine>();

    public void SetSalaryEvidence(string previewFingerprint, string frozenFingerprint, string reference)
    {
        if (CalculationVersion != "adjustments-v1" || LockedAt.HasValue || Lines.Count == 0)
            throw new InvalidOperationException("Salary evidence can be frozen once at preparation.");
        CalculationVersion = "salary-v2";
        PreviewFingerprint = ModelGuard.Required(previewFingerprint, 64, nameof(previewFingerprint));
        FrozenFingerprint = ModelGuard.Required(frozenFingerprint, 64, nameof(frozenFingerprint));
        CalculationPrivateReference = ModelGuard.Required(reference, 500, nameof(reference));
    }

    public void ReviewSalary(Guid requestId, string actor, DateTimeOffset at, bool approve)
    {
        if (CalculationVersion != "salary-v2" || ReviewState != "prepared" || LockedAt.HasValue || actor == PreparedByUserId || at < PreparedAt)
            throw new InvalidOperationException("Independent HR review of a prepared salary is required.");
        HrApprovalRequestId = ModelGuard.Id(requestId, nameof(requestId));
        HrApprovedByUserId = ModelGuard.Required(actor, 200, nameof(actor));
        HrApprovedAt = ModelGuard.Utc(at);
        ReviewState = approve ? "hr_approved" : "rejected";
    }

    public void CancelSalary(string actor)
    {
        if (CalculationVersion != "salary-v2" || ReviewState != "prepared" || actor != PreparedByUserId || LockedAt.HasValue)
            throw new InvalidOperationException("Only an unreviewed salary maker can cancel.");
        ReviewState = "cancelled";
    }

    public void RecordFinanceHandoff(string actor)
    {
        if (CalculationVersion != "salary-v2" || LockedAt is null || ReviewState is not ("locked" or "finance_handed_off") ||
            actor == PreparedByUserId || actor == HrApprovedByUserId || actor == LockedByUserId)
            throw new InvalidOperationException("Independent Finance handoff requires an approved salary.");
        ReviewState = "finance_handed_off";
    }

    /// <summary>Participates in the parent rowversion transaction for staged line additions.</summary>
    public void RecordLineChange()
    {
        if (LockedAt.HasValue) throw new InvalidOperationException("Locked handoff contents cannot change.");
        LinesRevision = checked(LinesRevision + 1);
    }

    public void Lock(Guid approvalRequestId, string actorUserId, DateTimeOffset lockedAt)
    {
        if (LockedAt.HasValue || lockedAt < PreparedAt) throw new InvalidOperationException("Batch can be locked once after preparation.");
        var approval = ModelGuard.Id(approvalRequestId, nameof(approvalRequestId));
        var actor = ModelGuard.Required(actorUserId, 200, nameof(actorUserId));
        if (CalculationVersion == "salary-v2" && (ReviewState != "hr_approved" || actor == PreparedByUserId || actor == HrApprovedByUserId || !HrApprovedAt.HasValue || lockedAt < HrApprovedAt))
            throw new InvalidOperationException("Independent Management approval must follow HR approval.");
        ApprovalRequestId = approval;
        LockedByUserId = actor;
        LockedAt = ModelGuard.Utc(lockedAt);
        if (CalculationVersion == "salary-v2") ReviewState = "locked";
    }
}
