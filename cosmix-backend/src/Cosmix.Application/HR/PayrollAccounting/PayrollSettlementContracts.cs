namespace Cosmix.Application.HR.PayrollAccounting;

/// <summary>Employee-safe payment metadata. No account numbers, receipt evidence or bank references.</summary>
public sealed record EmployeePaymentItem(Guid Id, decimal Amount, string State, DateOnly? ValueDate,
    DateTimeOffset CreatedAtUtc, DateTimeOffset? ConfirmedAtUtc, DateTimeOffset? ReversedAtUtc);
public sealed record EmployeePaymentPeriod(Guid BatchId, string BatchReference, DateOnly PeriodStart,
    DateOnly PeriodEndExclusive, string Currency, decimal NetEntitlement, decimal ConfirmedAmount,
    decimal PostedAmount, decimal PendingAmount, decimal FailedAmount, decimal ReversedAmount,
    decimal OutstandingAmount, decimal RemainingToInitiate, string PaymentState,
    IReadOnlyList<EmployeePaymentItem> Payments, int PaymentsPage, int PaymentsPageSize, bool HasMorePayments);
public sealed record EmployeePaymentPage(IReadOnlyList<EmployeePaymentPeriod> Items, int Page, int PageSize, bool HasMore);
public sealed record PayrollPaymentTotals(decimal Confirmed, decimal Posted, decimal Pending, decimal Failed,
    decimal Reversed, decimal Outstanding, decimal RemainingToInitiate, string State);
public sealed record PayrollSettlementEmployee(Guid EmployeeId, long EmployeeNumber, string EmployeeName,
    decimal GrossEarnings, decimal Deductions, decimal NetEntitlement, PayrollPaymentTotals Payments);
public sealed record PayrollSettlementComponent(Guid EmployeeId, string Component, decimal Amount);
public sealed record PayrollSettlementReport(Guid BatchId, string BatchReference, DateOnly PeriodStart,
    DateOnly PeriodEndExclusive, string Currency, string SourceFingerprint, DateTimeOffset GeneratedAtUtc,
    IReadOnlyList<PayrollSettlementEmployee> Employees, IReadOnlyList<PayrollSettlementComponent> Components,
    decimal GrossEarnings, decimal Deductions, decimal NetEntitlement, decimal ConfirmedPayments,
    decimal ReservedPayments, decimal OutstandingPayments, decimal PaymentLedgerNet,
    decimal PaymentLedgerDifference, IReadOnlyList<string> Issues);

public static class PayrollSettlementProjection
{
    public static PayrollPaymentTotals Calculate(decimal entitlement, IReadOnlyList<EmployeePaymentItem> payments)
    {
        if (entitlement < 0 || payments.Select(x => x.Id).Distinct().Count() != payments.Count ||
            payments.Any(x => x.Id == Guid.Empty || x.Amount <= 0 ||
                x.State is not ("Pending" or "Posted" or "Confirmed" or "Failed" or "Reversed") ||
                x.State == "Confirmed" && x.ConfirmedAtUtc is null || x.State == "Reversed" && x.ReversedAtUtc is null))
            throw new PayrollFinanceConflictException("Payment history contains inconsistent source records.");
        decimal Sum(string state) => payments.Where(x => x.State == state).Sum(x => x.Amount);
        var confirmed = Sum("Confirmed"); var posted = Sum("Posted"); var pending = Sum("Pending");
        var failed = Sum("Failed"); var reversed = Sum("Reversed");
        if (confirmed + posted + pending > entitlement)
            throw new PayrollFinanceConflictException("Payment reservations exceed the frozen employee entitlement.");
        var state = entitlement == 0 ? "NoPaymentDue" : confirmed == entitlement ? "Paid" :
            confirmed > 0 ? "PartiallyPaid" : posted + pending > 0 ? "Pending" :
            payments.OrderByDescending(x => x.ReversedAtUtc ?? x.ConfirmedAtUtc ?? x.CreatedAtUtc).FirstOrDefault()?.State switch
            {
                "Failed" => "Failed", "Reversed" => "Reversed", _ => "Unpaid"
            };
        return new(confirmed, posted, pending, failed, reversed, entitlement - confirmed,
            entitlement - confirmed - posted - pending, state);
    }
}
