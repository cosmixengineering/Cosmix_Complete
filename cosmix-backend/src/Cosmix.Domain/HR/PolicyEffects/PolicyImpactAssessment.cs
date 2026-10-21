using Cosmix.Domain.Common;
using Cosmix.Domain.Common.Approvals;
using Cosmix.Domain.HR.Employees;
using Cosmix.Domain.HR.Policies;

namespace Cosmix.Domain.HR.PolicyEffects;

/// <summary>An explainable proposed policy effect. Approval and payroll lock must be checked separately; this record never changes salary itself.</summary>
public sealed class PolicyImpactAssessment : IAppendOnlyEntity
{
    private PolicyImpactAssessment() { }

    public PolicyImpactAssessment(Guid employeeId, PolicyImpactKind kind, DateOnly periodStart, DateOnly periodEnd, int affectedMinutes, decimal? proposedMoneyImpact, string? currency, decimal? proposedKpiPercent, Guid policyVersionId, string calculationVersion, string protectedCalculationReference, Guid approvalRequestId, string recordedByUserId, DateTimeOffset recordedAt)
    {
        if (periodEnd < periodStart || proposedMoneyImpact.HasValue != (currency != null)) throw new ArgumentException("Impact period or money/currency pairing is invalid.");
        Id = Guid.NewGuid();
        EmployeeId = ModelGuard.Id(employeeId, nameof(employeeId));
        Kind = Enum.IsDefined(kind) ? kind : throw new ArgumentOutOfRangeException(nameof(kind));
        PeriodStart = periodStart;
        PeriodEnd = periodEnd;
        AffectedMinutes = HrModelGuard.NonNegative(affectedMinutes, nameof(affectedMinutes));
        ProposedMoneyImpact = proposedMoneyImpact.HasValue ? HrModelGuard.Money(proposedMoneyImpact.Value, nameof(proposedMoneyImpact)) : null;
        Currency = currency is null ? null : HrModelGuard.Currency(currency);
        ProposedKpiPercent = proposedKpiPercent.HasValue ? HrModelGuard.Percentage(proposedKpiPercent.Value, nameof(proposedKpiPercent)) : null;
        PolicyVersionId = ModelGuard.Id(policyVersionId, nameof(policyVersionId));
        CalculationVersion = ModelGuard.Required(calculationVersion, 80, nameof(calculationVersion));
        ProtectedCalculationReference = ModelGuard.Required(protectedCalculationReference, 500, nameof(protectedCalculationReference));
        ApprovalRequestId = ModelGuard.Id(approvalRequestId, nameof(approvalRequestId));
        RecordedByUserId = ModelGuard.Required(recordedByUserId, 200, nameof(recordedByUserId));
        RecordedAt = ModelGuard.Utc(recordedAt);
    }

    public Guid Id { get; private set; }
    public Guid EmployeeId { get; private set; }
    public PolicyImpactKind Kind { get; private set; }
    public DateOnly PeriodStart { get; private set; }
    public DateOnly PeriodEnd { get; private set; }
    public int AffectedMinutes { get; private set; }
    public decimal? ProposedMoneyImpact { get; private set; }
    public string? Currency { get; private set; }
    public decimal? ProposedKpiPercent { get; private set; }
    public Guid PolicyVersionId { get; private set; }
    public string CalculationVersion { get; private set; } = string.Empty;
    public string ProtectedCalculationReference { get; private set; } = string.Empty;
    public Guid ApprovalRequestId { get; private set; }
    public string RecordedByUserId { get; private set; } = string.Empty;
    public DateTimeOffset RecordedAt { get; private set; }
    public Employee Employee { get; private set; } = null!;
    public PolicyVersion PolicyVersion { get; private set; } = null!;
    public ApprovalRequest ApprovalRequest { get; private set; } = null!;
}
