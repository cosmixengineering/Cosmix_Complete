namespace Cosmix.Domain.HR.PayrollAccounting;

public enum PayrollObligationState
{
    Draft = 1,
    Finalized = 2,
    Voided = 3
}

public enum SalaryPaymentState
{
    Pending = 1,
    Posted = 2,
    Confirmed = 3,
    Failed = 4
}

public enum JournalEntryKind
{
    SalaryAccrual = 1,
    SalaryPayment = 2,
    StatutoryContribution = 3
}
