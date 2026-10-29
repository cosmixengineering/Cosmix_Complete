using Cosmix.Domain.HR.PayrollAccounting;
using Xunit;

namespace Cosmix.Tests;

public sealed class PayrollPaymentLifecycleTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 28, 0, 0, 0, TimeSpan.Zero);
    private static SalaryPayment Payment() => new(Guid.NewGuid(), Guid.NewGuid(), "TEST-RECEIPT", 50m, "maker", Now, Guid.NewGuid());

    [Fact]
    public void Posted_payment_cannot_release_entitlement_without_reversing_its_journal()
    {
        var payment = Payment();
        payment.RecordPaymentJournal(Guid.NewGuid());
        Assert.Throws<InvalidOperationException>(() => payment.MarkFailed("Rejected by bank"));
        Assert.Equal(SalaryPaymentState.Posted, payment.State);
    }

    [Fact]
    public void Confirmation_without_retained_proof_is_rejected()
    {
        var payment = Payment();
        payment.RecordPaymentJournal(Guid.NewGuid());
        Assert.Throws<InvalidOperationException>(() => payment.Confirm("checker", Now));
        Assert.Equal(SalaryPaymentState.Posted, payment.State);
    }

    [Fact]
    public void Zero_payment_cannot_be_reserved()
    {
        Assert.Throws<ArgumentException>(() => new SalaryPayment(Guid.NewGuid(), Guid.NewGuid(), "TEST", 0m, "maker", Now, Guid.NewGuid()));
    }

    [Fact]
    public void A_terminal_failure_cannot_be_rewritten()
    {
        var payment = Payment();
        payment.MarkFailed("original failure");
        Assert.Throws<InvalidOperationException>(() => payment.MarkFailed("different failure"));
        Assert.Equal("original failure", payment.FailureReason);
    }
}
