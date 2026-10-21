using Cosmix.Domain.Common;

namespace Cosmix.Domain.HR.AdvancesLoans;

public sealed class RepaymentScheduleEntry : IAppendOnlyEntity
{
    private RepaymentScheduleEntry() { }

    public RepaymentScheduleEntry(Guid advanceLoanAgreementId, int sequence, DateOnly dueDate, decimal principalAmount, decimal otherApprovedAmount, string currency)
    {
        if (otherApprovedAmount != 0) throw new ArgumentException("Employee loans are interest-free; additional charges are prohibited.", nameof(otherApprovedAmount));
        if (principalAmount <= 0) throw new ArgumentException("A scheduled principal repayment must be positive.", nameof(principalAmount));
        Id = Guid.NewGuid();
        AdvanceLoanAgreementId = ModelGuard.Id(advanceLoanAgreementId, nameof(advanceLoanAgreementId));
        Sequence = HrModelGuard.Positive(sequence, nameof(sequence));
        DueDate = dueDate;
        PrincipalAmount = HrModelGuard.Money(principalAmount, nameof(principalAmount));
        OtherApprovedAmount = HrModelGuard.Money(otherApprovedAmount, nameof(otherApprovedAmount));
        Currency = HrModelGuard.Currency(currency);
    }

    public Guid Id { get; private set; }
    public Guid AdvanceLoanAgreementId { get; private set; }
    public int Sequence { get; private set; }
    public DateOnly DueDate { get; private set; }
    public decimal PrincipalAmount { get; private set; }
    public decimal OtherApprovedAmount { get; private set; }
    public string Currency { get; private set; } = string.Empty;
    public AdvanceLoanAgreement AdvanceLoanAgreement { get; private set; } = null!;
}
