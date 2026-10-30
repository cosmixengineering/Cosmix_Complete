using Cosmix.Application.Authentication;
using Cosmix.Application.Attendance.Rosters;
using Cosmix.Application.HR.PayrollSalary;
using Cosmix.Domain.HR.Compensation;
using Xunit;

namespace Cosmix.Tests;

public sealed class PayrollReadinessCoverageTests
{
    private static readonly DateOnly From = new(2026, 10, 1), Until = new(2026, 11, 1);
    private static readonly Guid Employee = Guid.NewGuid();

    [Fact]
    public void Salary_coverage_requires_every_employed_day_and_does_not_resurrect_superseded_rates()
    {
        var first = Rate(1, From, null);
        var second = Rate(2, From.AddDays(10), From.AddDays(20), first.Id);
        Assert.False(PayrollReadinessCoverage.Salary([first, second], From, Until));
        Assert.True(PayrollReadinessCoverage.Salary([first, second], From, From.AddDays(20)));
        Assert.False(PayrollReadinessCoverage.Salary([Rate(1, From.AddDays(2), null)], From, Until));
        Assert.True(PayrollReadinessCoverage.Salary([Rate(1, From.AddDays(2), null)], From.AddDays(2), Until));
        Assert.False(PayrollReadinessCoverage.Salary([], From, Until));
    }

    [Theory]
    [InlineData(WageBasis.Monthly)]
    [InlineData(WageBasis.Daily)]
    [InlineData(WageBasis.Hourly)]
    public void Salary_coverage_supports_all_approved_wage_bases(WageBasis basis) =>
        Assert.True(PayrollReadinessCoverage.Salary([Rate(1, From, null, basis: basis)], From, Until));

    [Fact]
    public async Task Schedule_checks_use_effective_roster_including_rest_days_and_report_missing_setup()
    {
        var resolver = new Resolver((_, date) => new(Employee, date, date.Day % 7 == 0 ? "rest_day" : "scheduled", "Asia/Karachi"));
        Assert.True(await PayrollReadinessCoverage.RosterAsync(resolver, Employee, From, Until, default));
        Assert.Equal(31, resolver.Dates.Count);
        var missing = new Resolver((_, date) => date.Day == 20 ? throw new AuthenticationFailure("staff_taxonomy_category_not_configured", 503) : new(Employee, date, "scheduled", "Asia/Karachi"));
        Assert.False(await PayrollReadinessCoverage.RosterAsync(missing, Employee, From, Until, default));
        var unavailable = new Resolver((_, date) => new(Employee, date, "site_not_assigned", "Asia/Karachi"));
        Assert.False(await PayrollReadinessCoverage.RosterAsync(unavailable, Employee, From, Until, default));
    }

    [Fact]
    public async Task Readiness_never_masks_authorization_or_corrupt_schedule_failures()
    {
        var resolver = new Resolver((_, _) => throw new AuthenticationFailure("employee_assignment_history_conflict", 409));
        var error = await Assert.ThrowsAsync<AuthenticationFailure>(() => PayrollReadinessCoverage.RosterAsync(resolver, Employee, From, Until, default));
        Assert.Equal("employee_assignment_history_conflict", error.Code);
    }

    private static CompensationRecord Rate(int revision, DateOnly from, DateOnly? until, Guid? previous = null,
        WageBasis basis = WageBasis.Monthly) => new(Employee, revision, previous, 100m, 100m, "PKR", basis,
            PaymentMethod.Bank, from, until, revision == 1 ? null : Guid.NewGuid(), "synthetic", DateTimeOffset.UtcNow);

    private sealed class Resolver(Func<Guid, DateOnly, EffectiveRosterResponse> resolve) : IIndividualRosterResolver
    {
        public List<DateOnly> Dates { get; } = [];
        public Task<EffectiveRosterResponse> ResolveAsync(Guid employeeId, DateOnly businessDate, CancellationToken ct)
        { Dates.Add(businessDate); return Task.FromResult(resolve(employeeId, businessDate)); }
    }
}
