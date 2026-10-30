using System.Reflection;
using System.Text.Json;
using Cosmix.Application.Authentication;
using Cosmix.Application.HR.PayrollAccounting;
using Cosmix.Application.HR.PayrollSalary;
using Cosmix.Domain.HR.PayrollAccounting;
using Cosmix.Domain.HR.StatutoryContributions;
using Xunit;

namespace Cosmix.Tests;

public sealed class PayrollPostingReadinessTests
{
    private static readonly DateOnly Month = new(2026, 10, 1);

    [Theory]
    [InlineData("inactive")]
    [InlineData("missing")]
    [InlineData("wrong-kind")]
    public async Task Unrelated_active_account_cannot_replace_profile_salary_account(string failure)
    {
        var salary = new PayrollLedgerAccount("SAL", "Salary", failure == "wrong-kind" ? PayrollAccountKind.Asset : PayrollAccountKind.Expense);
        if (failure == "inactive") salary.SetActive(false);
        var accounts = new List<PayrollLedgerAccount> { new("PAY", "Payable", PayrollAccountKind.Liability),
            new("OTHER", "Unrelated", PayrollAccountKind.Expense) };
        if (failure != "missing") accounts.Add(salary);
        Assert.Equal(0, await Count(Profile(), accounts));
    }

    [Fact]
    public async Task Profile_must_cover_whole_month_in_payroll_currency()
    {
        Assert.Equal(0, await Count(Profile(until: Month.AddDays(15)), Accounts()));
        Assert.Equal(0, await Count(Profile(currency: "USD"), Accounts()));
        Assert.Equal(1, await Count(Profile(until: Month.AddMonths(1)), Accounts()));
    }

    [Fact]
    public async Task Deduction_and_statutory_accounts_obey_actual_posting_compatibility_rules()
    {
        var loan = new PayrollLedgerAccount("LOAN", "Loan", PayrollAccountKind.Liability);
        var loanRules = new PayrollPostingRules("SAL", "PAY", [new("LoanRepayment", "LOAN")], []);
        Assert.Equal(0, await Count(Profile(rules: loanRules), [.. Accounts(), loan]));
        var contraRules = new PayrollPostingRules("SAL", "PAY", [new("AttendanceDeduction", "SAL")], []);
        Assert.Equal(0, await Count(Profile(rules: contraRules), Accounts()));
        var statutoryRules = new PayrollPostingRules("SAL", "PAY", [], [new(StatutoryScheme.EOBI, "SAL", "PAY")]);
        Assert.Equal(0, await Count(Profile(rules: statutoryRules), Accounts()));
        Assert.Equal(1, await Count(Profile(rules: loanRules), [.. Accounts(), new("LOAN", "Loan", PayrollAccountKind.Asset)]));
    }

    [Fact]
    public async Task Repository_failure_is_not_disguised_as_missing_company_configuration()
    {
        var repository = DispatchProxy.Create<IPayrollFinanceRepository, AccountRepository>();
        ((AccountRepository)(object)repository).Failure = new AuthenticationFailure("access_denied", 403);
        var error = await Assert.ThrowsAsync<AuthenticationFailure>(() => PayrollPostingReadiness.CountApplicableAsync(
            [Profile()], new PayrollFinanceSetupService(repository), Month, default));
        Assert.Equal("access_denied", error.Code);
    }

    private static PayrollLedgerAccount[] Accounts() => [new("SAL", "Salary", PayrollAccountKind.Expense), new("PAY", "Payable", PayrollAccountKind.Liability)];
    private static PayrollPostingProfile Profile(DateOnly? until = null, string currency = "PKR", PayrollPostingRules? rules = null)
    {
        var at = new DateTimeOffset(2026, 9, 1, 0, 0, 0, TimeSpan.Zero);
        var profile = new PayrollPostingProfile("Synthetic", currency, Month, until,
            JsonSerializer.Serialize(rules ?? new PayrollPostingRules("SAL", "PAY", [], [])), "maker", at);
        profile.Approve("checker", at.AddDays(1));
        return profile;
    }
    private static Task<int> Count(PayrollPostingProfile profile, IReadOnlyList<PayrollLedgerAccount> accounts)
    {
        var repository = DispatchProxy.Create<IPayrollFinanceRepository, AccountRepository>();
        ((AccountRepository)(object)repository).Accounts = accounts;
        return PayrollPostingReadiness.CountApplicableAsync([profile], new PayrollFinanceSetupService(repository), Month, default);
    }
    public class AccountRepository : DispatchProxy
    {
        public IReadOnlyList<PayrollLedgerAccount> Accounts { get; set; } = [];
        public Exception? Failure { get; set; }
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            if (Failure is not null) throw Failure;
            if (targetMethod?.Name == "AccountAsync" && args?[0] is string code)
                return Task.FromResult(Accounts.SingleOrDefault(x => x.Code == code));
            throw new InvalidOperationException("Readiness must not mutate Finance records.");
        }
    }
}
