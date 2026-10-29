using System.Text;
using Cosmix.Application.Authentication;
using Cosmix.Application.Finance.Reconciliation;
using Xunit;

namespace Cosmix.Tests;

public sealed class BankReconciliationTests
{
    private static readonly DateOnly Month = new(2026, 10, 1);
    [Fact]
    public void Statement_mapping_is_explicit_and_duplicate_or_unbalanced_sources_are_rejected()
    {
        var mapping = new BankStatementMapping("CSV", "Date", "Ref", "Debit", "Credit", "yyyy-MM-dd");
        var bytes = Encoding.UTF8.GetBytes("Date,Ref,Debit,Credit\n2026-10-02,PAY-1,100,\n2026-10-04,REV-1,,20\n");
        var result = BankStatementParser.Parse("bank.csv", bytes, mapping, Month, 500m, 420m);
        Assert.Equal(-100m, result[0].Amount);
        Assert.Equal(20m, result[1].Amount);
        Assert.Throws<AuthenticationFailure>(() => BankStatementParser.Parse("bank.csv", bytes, mapping, Month, 500m, 419m));
        var duplicate = Encoding.UTF8.GetBytes("Date,Ref,Debit,Credit\n2026-10-02,PAY-1,100,\n2026-10-02,PAY-1,100,\n");
        Assert.Throws<AuthenticationFailure>(() => BankStatementParser.Parse("bank.csv", duplicate, mapping, Month, 500m, 300m));
    }

    [Fact]
    public void Complete_matching_supports_bulk_transfers_and_reversals_without_approving_payments()
    {
        var a = Guid.NewGuid(); var b = Guid.NewGuid(); var reverse = Guid.NewGuid();
        BankStatementRow[] rows = [new(1, Month.AddDays(1), "BULK-1", -300m), new(2, Month.AddDays(3), "REV-1", 100m)];
        BankLedgerMovement[] ledger = [new(a, Month.AddDays(1), "BULK-1", -100m, "PKR"), new(b, Month.AddDays(1), "BULK-1", -200m, "PKR"), new(reverse, Month.AddDays(3), "REV-1", 100m, "PKR")];
        BankRowResolution[] matches = [new(1, [a, b], null, null), new(2, [reverse], null, null)];
        var report = BankReconciliationProjection.Check(rows, ledger, matches, "PKR");
        Assert.True(report.CanClose);
        Assert.Equal(-200m, report.MatchedBankNet);
        Assert.Equal(report.MatchedBankNet, report.MatchedLedgerNet);
        Assert.Empty(report.UnmatchedLedgerIds);
    }

    [Fact]
    public void Partial_ambiguous_wrong_amount_currency_or_reused_matches_cannot_close()
    {
        var id = Guid.NewGuid();
        BankStatementRow[] rows = [new(1, Month, "PAY-1", -100m)];
        BankLedgerMovement[] ledger = [new(id, Month, "PAY-1", -100m, "PKR")];
        Assert.False(BankReconciliationProjection.Check(rows, ledger, [], "PKR").CanClose);
        Assert.Throws<AuthenticationFailure>(() => BankReconciliationProjection.Check(rows, ledger, [new(1, [id, id], null, null)], "PKR"));
        Assert.Throws<AuthenticationFailure>(() => BankReconciliationProjection.Check(rows, [ledger[0] with { Amount = -99m }], [new(1, [id], null, null)], "PKR"));
        Assert.Throws<AuthenticationFailure>(() => BankReconciliationProjection.Check(rows, [ledger[0] with { Currency = "USD" }], [new(1, [id], null, null)], "PKR"));
        Assert.Throws<AuthenticationFailure>(() => BankReconciliationProjection.Check(rows, [ledger[0] with { Reference = "OTHER" }], [new(1, [id], null, null)], "PKR"));
        Assert.True(BankReconciliationProjection.Check(rows, [ledger[0] with { Reference = "OTHER" }], [new(1, [id], null, "Verified bank batch reference against retained export")], "PKR").CanClose);
        var outside = BankReconciliationProjection.Check(rows, [], [new(1, [], "Non-payroll bank charge checked against statement", null)], "PKR");
        Assert.True(outside.CanClose);
        Assert.Equal(-100m, outside.OutsidePayrollNet);
    }
}
