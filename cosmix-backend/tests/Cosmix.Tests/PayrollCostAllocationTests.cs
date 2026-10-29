using Cosmix.Application.Authentication;
using Cosmix.Application.Finance.Costing;
using Xunit;

namespace Cosmix.Tests;

public sealed class PayrollCostAllocationTests
{
    [Fact]
    public void Gross_cost_is_allocated_to_projects_and_sites_without_losing_rounding_cents()
    {
        var employee = Guid.NewGuid(); var project = Guid.NewGuid(); var site = Guid.NewGuid();
        PayrollCostShare[] shares = [new(employee, project, site, 3333), new(employee, project, null, 3333), new(employee, null, null, 3334)];
        var result = PayrollCostAllocation.Allocate([new(employee, 100.01m, "PKR")], shares);
        Assert.Equal(100.01m, result.Sum(x => x.Amount));
        Assert.Equal(33.35m, Assert.Single(result, x => x.ProjectId is null).Amount);
        Assert.All(result, x => Assert.Equal(decimal.Round(x.Amount, 2), x.Amount));
        var reversed = PayrollCostAllocation.Allocate([new(employee, 100.01m, "PKR")], shares.Reverse().ToArray());
        Assert.Equal(result, reversed);
    }

    [Fact]
    public void Incomplete_duplicate_or_foreign_employee_allocations_cannot_be_approved()
    {
        var employee = Guid.NewGuid(); PayrollCostBasis[] basis = [new(employee, 1000m, "PKR")];
        Assert.Throws<AuthenticationFailure>(() => PayrollCostAllocation.Allocate(basis, []));
        Assert.Throws<AuthenticationFailure>(() => PayrollCostAllocation.Allocate(basis, [new(employee, null, null, 9999)]));
        Assert.Throws<AuthenticationFailure>(() => PayrollCostAllocation.Allocate(basis, [new(Guid.NewGuid(), null, null, 10000)]));
        Assert.Throws<AuthenticationFailure>(() => PayrollCostAllocation.Allocate(basis, [new(employee, null, null, 5000), new(employee, null, null, 5000)]));
    }

    [Fact]
    public void Every_employee_and_currency_retains_its_own_gross_cost()
    {
        var first = Guid.NewGuid(); var second = Guid.NewGuid();
        var result = PayrollCostAllocation.Allocate([new(first, 2500m, "PKR"), new(second, 20m, "USD")],
            [new(first, null, null, 10000), new(second, null, null, 10000)]);
        Assert.Equal(2500m, Assert.Single(result, x => x.EmployeeId == first).Amount);
        Assert.Equal("USD", Assert.Single(result, x => x.EmployeeId == second).Currency);
    }
}
