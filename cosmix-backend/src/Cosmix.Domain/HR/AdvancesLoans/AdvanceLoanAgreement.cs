using Cosmix.Domain.Common;
using Cosmix.Domain.Common.Approvals;

namespace Cosmix.Domain.HR.AdvancesLoans;

/// <summary>Approved terms are retained without assuming interest, repayment rates or a salary-deduction formula.</summary>
public sealed class AdvanceLoanAgreement : IAppendOnlyEntity
{
    private AdvanceLoanAgreement() { }

    public AdvanceLoanAgreement(Guid advanceLoanRequestId, decimal approvedPrincipal, string currency, DateOnly effectiveOn, string approvedTermsPrivateReference, Guid approvalRequestId, string recordedByUserId, DateTimeOffset recordedAt)
    {
        if (approvedPrincipal <= 0) throw new ArgumentOutOfRangeException(nameof(approvedPrincipal));
        Id = Guid.NewGuid();
        AdvanceLoanRequestId = ModelGuard.Id(advanceLoanRequestId, nameof(advanceLoanRequestId));
        ApprovedPrincipal = HrModelGuard.Money(approvedPrincipal, nameof(approvedPrincipal));
        Currency = HrModelGuard.Currency(currency);
        EffectiveOn = effectiveOn;
        ApprovedTermsPrivateReference = ModelGuard.Required(approvedTermsPrivateReference, 500, nameof(approvedTermsPrivateReference));
        ApprovalRequestId = ModelGuard.Id(approvalRequestId, nameof(approvalRequestId));
        RecordedByUserId = ModelGuard.Required(recordedByUserId, 200, nameof(recordedByUserId));
        RecordedAt = ModelGuard.Utc(recordedAt);
    }

    public Guid Id { get; private set; }
    public Guid AdvanceLoanRequestId { get; private set; }
    public decimal ApprovedPrincipal { get; private set; }
    public string Currency { get; private set; } = string.Empty;
    public DateOnly EffectiveOn { get; private set; }
    public string ApprovedTermsPrivateReference { get; private set; } = string.Empty;
    public Guid ApprovalRequestId { get; private set; }
    public string RecordedByUserId { get; private set; } = string.Empty;
    public DateTimeOffset RecordedAt { get; private set; }
    public AdvanceLoanRequest AdvanceLoanRequest { get; private set; } = null!;
    public ApprovalRequest ApprovalRequest { get; private set; } = null!;

    public ICollection<RepaymentScheduleEntry> Schedule { get; private set; } = new List<RepaymentScheduleEntry>();
}
