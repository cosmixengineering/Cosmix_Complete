using System.Security.Claims;
using System.Text;
using Cosmix.Api.Controllers.HR;
using Cosmix.Application.Authentication;
using Cosmix.Application.HR.PayrollReconciliation;
using Cosmix.Infrastructure.Database;
using Cosmix.Infrastructure.Repositories.HR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Cosmix.Tests;

public sealed class PayrollReconciliationAvailabilityTests
{
    [Theory]
    [InlineData("historical")]
    [InlineData("get")]
    [InlineData("active")]
    [InlineData("save")]
    public async Task Production_repository_cannot_fabricate_salary_or_volatile_signoff(string action)
    {
        using var db = new CosmixDbContext(new DbContextOptionsBuilder<CosmixDbContext>().Options);
        var repository = new PayrollReconciliationRepository(db);
        await Unavailable(() => action switch
        {
            "historical" => repository.GetErpPayrollRecordsAsync(2026, 9, default),
            "get" => repository.GetByIdAsync(Guid.NewGuid(), default),
            "active" => repository.GetActiveByPeriodAsync(2026, 9, default),
            _ => repository.SaveRunAsync(Run(), default)
        });
    }

    [Theory]
    [InlineData("historical")]
    [InlineData("initiate")]
    [InlineData("hr")]
    [InlineData("finance")]
    [InlineData("management")]
    public async Task Controller_using_production_repository_never_returns_fake_comparison_or_approval(string action)
    {
        using var db = new CosmixDbContext(new DbContextOptionsBuilder<CosmixDbContext>().Options);
        var repository = new PayrollReconciliationRepository(db);
        var historical = new HistoricalPayrollReconciliationService(repository, TimeProvider.System);
        var parallel = new ParallelPayrollService(repository, historical, TimeProvider.System);
        var controller = new PayrollReconciliationController(historical, parallel)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString())], "test"))
            } }
        };
        var bytes = Convert.ToBase64String(Encoding.UTF8.GetBytes("EmployeeCode,BasicSalary\n101,31000.00\n"));
        Dictionary<string, string> mapping = new() { ["EmployeeCode"] = "EmployeeCode", ["BasicSalary"] = "BasicSalary" };
        await Unavailable(() => action switch
        {
            "historical" => controller.ReconcileHistorical(new("salary.csv", "text/csv", bytes, 2026, 9, mapping, "Synthetic comparison"), default),
            "initiate" => controller.InitiateParallel(new(2026, 9, null, "salary.csv", bytes, mapping, "Synthetic comparison"), default),
            "hr" => controller.HrReview(new(Guid.NewGuid(), true, [], "Synthetic review", 1), default),
            "finance" => controller.FinanceReview(new(Guid.NewGuid(), true, true, true, "Synthetic review", 1), default),
            _ => controller.ManagementApproval(new(Guid.NewGuid(), true, "Synthetic review", 1), default)
        });
    }

    private static async Task Unavailable(Func<Task> action)
    {
        var failure = await Assert.ThrowsAsync<AuthenticationFailure>(action);
        Assert.Equal(503, failure.StatusCode);
        Assert.Equal("payroll_reconciliation_workflow_not_available", failure.Code);
    }

    private static ParallelPayrollRunRecord Run() => new(Guid.NewGuid(), 2026, 9,
        ParallelPayrollRunState.ManagementApproved, 4, "maker", DateTimeOffset.UtcNow,
        "hr", DateTimeOffset.UtcNow, "review", "finance", DateTimeOffset.UtcNow, "review",
        "management", DateTimeOffset.UtcNow, "review", new string('A', 64),
        new(1, 1, 0, 31000, 31000, 0, 31000, 31000, 0, true), []);
}
