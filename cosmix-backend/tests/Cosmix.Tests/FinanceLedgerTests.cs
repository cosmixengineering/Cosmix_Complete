using Cosmix.Application.Authentication;
using Cosmix.Application.Finance.Ledger;
using Xunit;

namespace Cosmix.Tests;

public sealed class FinanceLedgerTests
{
    private static readonly DateOnly From = new(2026, 10, 1), Until = new(2026, 11, 1);

    [Fact]
    public void Trial_balance_includes_opening_payroll_payment_and_reversal_without_mixing_currencies()
    {
        var rows = Journal(From.AddDays(-1), "SAL", "PAY", 1000m)
            .Concat(Journal(From, "PAY", "BANK", 800m))
            .Concat(Journal(From.AddDays(1), "BANK", "PAY", 200m))
            .Concat(Journal(From, "SAL", "PAY", 10m, "USD"))
            .Concat(Journal(Until, "PAY", "BANK", 400m)).ToArray();
        var result = FinanceTrialBalanceProjection.Calculate(From, Until, rows);
        var salary = Assert.Single(result.Accounts, x => x.Currency == "PKR" && x.AccountCode == "SAL");
        Assert.Equal(1000m, salary.OpeningBalance); Assert.Equal(0m, salary.Debit);
        var payable = Assert.Single(result.Accounts, x => x.Currency == "PKR" && x.AccountCode == "PAY");
        Assert.Equal(-1000m, payable.OpeningBalance); Assert.Equal(800m, payable.Debit);
        Assert.Equal(200m, payable.Credit); Assert.Equal(-400m, payable.ClosingBalance);
        Assert.All(result.Accounts.GroupBy(x => x.Currency), group => Assert.Equal(0m, group.Sum(x => x.ClosingBalance)));
        Assert.Equal(10m, Assert.Single(result.Accounts, x => x.Currency == "USD" && x.AccountCode == "SAL").Debit);
    }

    [Fact]
    public void Missing_journal_side_and_duplicate_source_fail_instead_of_showing_a_balanced_report()
    {
        var rows = Journal(From, "SAL", "PAY", 1000m).ToArray();
        Assert.Throws<AuthenticationFailure>(() => FinanceTrialBalanceProjection.Calculate(From, Until, [rows[0]]));
        Assert.Throws<AuthenticationFailure>(() => FinanceTrialBalanceProjection.Calculate(From, Until, rows.Concat(rows).ToArray()));
        Assert.Throws<AuthenticationFailure>(() => FinanceTrialBalanceProjection.Calculate(From, Until,
            [rows[0], rows[1] with { Credit = 900m }]));
    }

    [Fact]
    public void Missing_posting_date_is_not_silently_dropped_from_finance()
    {
        var rows = Journal(From, "SAL", "PAY", 1000m).Select(x => x with { PostingDate = null }).ToArray();
        Assert.Throws<AuthenticationFailure>(() => FinanceTrialBalanceProjection.Calculate(From, Until, rows));
    }

    private static IEnumerable<FinanceLedgerEntry> Journal(DateOnly date, string debit, string credit, decimal amount, string currency = "PKR")
    {
        var id = Guid.NewGuid(); var obligation = Guid.NewGuid();
        yield return new(id, "Payroll", id.ToString(), date, currency, debit, amount, 0, obligation);
        yield return new(id, "Payroll", id.ToString(), date, currency, credit, 0, amount, obligation);
    }
}
