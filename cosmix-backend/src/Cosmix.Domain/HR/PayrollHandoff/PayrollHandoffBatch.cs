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
    public byte[] RowVersion { get; private set; } = [];

    public DateTimeOffset? LockedAt { get; private set; }
    public string? LockedByUserId { get; private set; }
    public Guid? ApprovalRequestId { get; private set; }
    public Cosmix.Domain.Common.Approvals.ApprovalRequest? ApprovalRequest { get; private set; }
    public ICollection<PayrollHandoffLine> Lines { get; private set; } = new List<PayrollHandoffLine>();

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
        ApprovalRequestId = approval; LockedByUserId = actor; LockedAt = ModelGuard.Utc(lockedAt);
    }
}
