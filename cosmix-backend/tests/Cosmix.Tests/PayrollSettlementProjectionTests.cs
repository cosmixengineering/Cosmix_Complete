using Cosmix.Application.HR.PayrollAccounting;
using Xunit;

namespace Cosmix.Tests;
public sealed class PayrollSettlementProjectionTests
{
    private static EmployeePaymentItem Payment(decimal amount, string state) => new(Guid.NewGuid(), amount, state,
        new(2026, 9, 30), DateTimeOffset.UtcNow, state == "Confirmed" ? DateTimeOffset.UtcNow : null,
        state == "Reversed" ? DateTimeOffset.UtcNow : null);

    [Fact]
    public void Confirmed_is_paid_but_posted_and_pending_only_reserve_entitlement()
    {
        var result = PayrollSettlementProjection.Calculate(1000,
            [Payment(200, "Confirmed"), Payment(300, "Posted"), Payment(100, "Pending")]);
        Assert.Equal(800, result.Outstanding);
        Assert.Equal(400, result.RemainingToInitiate);
        Assert.Equal("PartiallyPaid", result.State);
    }
    [Fact]
    public void Historical_failed_and_reversed_attempts_do_not_consume_the_new_balance()
    {
        var result = PayrollSettlementProjection.Calculate(1000,
            [Payment(1000, "Failed"), Payment(1000, "Reversed"), Payment(1000, "Confirmed")]);
        Assert.Equal("Paid", result.State); Assert.Equal(0, result.Outstanding);
        Assert.Equal(1000, result.Failed); Assert.Equal(1000, result.Reversed);
    }
    [Fact]
    public void An_over_reserved_or_unknown_payment_cannot_be_displayed_as_valid()
    {
        Assert.Throws<PayrollFinanceConflictException>(() => PayrollSettlementProjection.Calculate(100,
            [Payment(75, "Posted"), Payment(50, "Pending")]));
        Assert.Throws<PayrollFinanceConflictException>(() => PayrollSettlementProjection.Calculate(100, [Payment(1, "Unknown")]));
    }
    [Fact]
    public void Duplicate_payment_ids_are_rejected_and_zero_entitlement_is_explicit()
    {
        var item = Payment(10, "Confirmed");
        Assert.Throws<PayrollFinanceConflictException>(() => PayrollSettlementProjection.Calculate(100, [item, item]));
        Assert.Equal("NoPaymentDue", PayrollSettlementProjection.Calculate(0, []).State);
        Assert.Equal("Unpaid", PayrollSettlementProjection.Calculate(100, []).State);
    }
}
