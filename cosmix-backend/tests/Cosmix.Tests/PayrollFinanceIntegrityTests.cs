using Cosmix.Domain.HR.StatutoryContributions;
using Cosmix.Domain.HR.PayrollAccounting;
using Xunit;

namespace Cosmix.Tests;

public sealed class PayrollFinanceIntegrityTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 1, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void HeaderOnlyContributionCannotBeApproved()
    {
        var contribution = new StatutoryContribution(StatutoryScheme.EOBI, new DateOnly(2026, 10, 1), 100, 500, "PKR", "maker", Now);
        Assert.Throws<InvalidOperationException>(() => contribution.Approve("reviewer", Now));
    }

    [Fact]
    public void ContributionRequiresExactEmployeeTotalsAndUniqueEmployees()
    {
        var contribution = new StatutoryContribution(StatutoryScheme.EOBI, new DateOnly(2026, 10, 1), 100, 500, "PKR", "maker", Now, hasEmployeeEntries: true);
        var employee = Guid.NewGuid();
        contribution.Entries.Add(Entry(contribution.Id, employee, 100, 500));
        contribution.Entries.Add(Entry(contribution.Id, employee, 0, 0));
        Assert.Throws<InvalidOperationException>(() => contribution.Approve("reviewer", Now));
    }

    [Fact]
    public void IndependentApprovalPreservesEmployeeEmployerSeparation()
    {
        var contribution = new StatutoryContribution(StatutoryScheme.EOBI, new DateOnly(2026, 10, 1), 100, 500, "PKR", "maker", Now, hasEmployeeEntries: true);
        contribution.Entries.Add(Entry(contribution.Id, Guid.NewGuid(), 100, 500));
        Assert.Throws<InvalidOperationException>(() => contribution.Approve("maker", Now));
        contribution.Approve("reviewer", Now);
        Assert.Equal(100, contribution.EmployeeContributionAmount);
        Assert.Equal(500, contribution.EmployerContributionAmount);
        Assert.Throws<InvalidOperationException>(() => contribution.BindObligation(Guid.NewGuid()));
        Assert.Null(contribution.ObligationId);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("[{\"EmployeeContribution\":100,\"EmployerContribution\":500}]")]
    public void EntryFlagOrLegacyJsonCannotReplaceActualEmployeeEntries(string? legacyJson)
    {
        var row = new StatutoryContribution(StatutoryScheme.EOBI, new(2026, 10, 1), 100, 500, "PKR", "maker", Now,
            employeeItemsJson: legacyJson, hasEmployeeEntries: true);
        Assert.Throws<InvalidOperationException>(() => row.Approve("reviewer", Now));
        Assert.Equal(ContributionState.Draft, row.State);
        Assert.Null(row.ApprovedAt);
    }

    [Fact]
    public void ApprovedContributionCanOnlyBindItsObligationThroughValidatedPosting()
    {
        var row = new StatutoryContribution(StatutoryScheme.EOBI, new(2026, 10, 1), 100, 500, "PKR", "maker", Now, hasEmployeeEntries: true);
        row.Entries.Add(Entry(row.Id, Guid.NewGuid(), 100, 500));
        row.Approve("reviewer", Now);
        var obligation = Guid.NewGuid(); var journal = Guid.NewGuid();
        row.RecordPosting(obligation, journal, "STAT", "finance", Now);
        Assert.Equal(obligation, row.ObligationId); Assert.Equal(journal, row.JournalEntryId);
        Assert.Equal(ContributionState.Posted, row.State);
        Assert.Throws<InvalidOperationException>(() => row.BindObligation(Guid.NewGuid()));
    }

    [Fact]
    public void ApprovedPeriodCannotReopenAfterClosing()
    {
        var period = new PayrollAccountingPeriod(new DateOnly(2026, 10, 1), new DateOnly(2026, 11, 1), "PKR", "maker", Now);
        Assert.Throws<InvalidOperationException>(() => period.Approve("maker", Now));
        period.Approve("reviewer", Now);
        period.Close("reviewer", Now);
        Assert.Throws<InvalidOperationException>(() => period.Approve("reviewer", Now));
    }

    private static StatutoryContributionEntry Entry(Guid contributionId, Guid employeeId, decimal employee, decimal employer) =>
        new(contributionId, employeeId, Guid.NewGuid(), "REG-TEST", employee, employer, "synthetic-evidence", new string('A', 64), "private-test-proof");
}
