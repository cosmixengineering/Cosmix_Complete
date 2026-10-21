using Cosmix.Domain.Common;

namespace Cosmix.Domain.HR.AdvancesLoans;

/// <summary>Positive confirmed repayment reduces outstanding principal; reversal is negative. HR evidence is reconciled to the agreement and is not a finance general ledger.</summary>
public sealed class RepaymentLedgerEntry : IAppendOnlyEntity
{
    private RepaymentLedgerEntry() { }

    public RepaymentLedgerEntry(Guid advanceLoanAgreementId, Guid? repaymentScheduleEntryId, RepaymentEntryKind kind, decimal amount, string currency, DateOnly valueDate, string sourceReference, string idempotencyKey, Guid? reversesEntryId, string recordedByUserId, DateTimeOffset recordedAt)
    {
        if (amount == 0 || ((kind is RepaymentEntryKind.PayrollDeduction or RepaymentEntryKind.ReceivedPayment) && amount < 0) || kind == RepaymentEntryKind.Reversal && amount > 0) throw new ArgumentOutOfRangeException(nameof(amount));
        if (kind == RepaymentEntryKind.Reversal && !reversesEntryId.HasValue)
            throw new ArgumentException("A repayment reversal must identify the original ledger entry.", nameof(reversesEntryId));
        Id = Guid.NewGuid();
        AdvanceLoanAgreementId = ModelGuard.Id(advanceLoanAgreementId, nameof(advanceLoanAgreementId));
        RepaymentScheduleEntryId = repaymentScheduleEntryId.HasValue ? ModelGuard.Id(repaymentScheduleEntryId.Value, nameof(repaymentScheduleEntryId)) : null;
        Kind = Enum.IsDefined(kind) ? kind : throw new ArgumentOutOfRangeException(nameof(kind));
        Amount = HrModelGuard.SignedMoney(amount, nameof(amount));
        Currency = HrModelGuard.Currency(currency);
        ValueDate = valueDate;
        SourceReference = ModelGuard.Required(sourceReference, 150, nameof(sourceReference));
        IdempotencyKey = ModelGuard.Required(idempotencyKey, 100, nameof(idempotencyKey));
        ReversesEntryId = reversesEntryId.HasValue ? ModelGuard.Id(reversesEntryId.Value, nameof(reversesEntryId)) : null;
        RecordedByUserId = ModelGuard.Required(recordedByUserId, 200, nameof(recordedByUserId));
        RecordedAt = ModelGuard.Utc(recordedAt);
    }

    public Guid Id { get; private set; }
    public Guid AdvanceLoanAgreementId { get; private set; }
    public Guid? RepaymentScheduleEntryId { get; private set; }
    public RepaymentEntryKind Kind { get; private set; }
    public decimal Amount { get; private set; }
    public string Currency { get; private set; } = string.Empty;
    public DateOnly ValueDate { get; private set; }
    public string SourceReference { get; private set; } = string.Empty;
    public string IdempotencyKey { get; private set; } = string.Empty;
    public Guid? ReversesEntryId { get; private set; }
    public string RecordedByUserId { get; private set; } = string.Empty;
    public DateTimeOffset RecordedAt { get; private set; }
    public AdvanceLoanAgreement AdvanceLoanAgreement { get; private set; } = null!;
    public RepaymentScheduleEntry? RepaymentScheduleEntry { get; private set; }
    public RepaymentLedgerEntry? ReversesEntry { get; private set; }
}
