using Cosmix.Application.HR.PayrollSalary;
using Cosmix.Infrastructure.Database;
using Cosmix.Infrastructure.Repositories.HR;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Cosmix.Tests;

public sealed class CompanyPayrollReadinessTests
{
    private sealed class FakeReadinessRepository : ICompanyPayrollReadinessRepository
    {
        public List<ActiveEmployeeReadinessDto> ActiveEmployees { get; set; } = [];
        public HashSet<Guid> EmployeesWithSalary { get; set; } = [];
        public HashSet<Guid> EmployeesWithShift { get; set; } = [];
        public HashSet<Guid> EmployeesWithBank { get; set; } = [];
        public HashSet<Guid> EmployeesWithStatutory { get; set; } = [];
        public int ActivePostingProfilesCount { get; set; } = 1;
        public int ActiveAccountsCount { get; set; } = 5;
        public int PendingPolicyApprovalsCount { get; set; } = 0;
        public int ApprovedSalaryPolicyCount { get; set; } = 1;

        public Task<ReadinessAuditData> FetchReadinessDataAsync(DateOnly targetMonth, CancellationToken ct) =>
            Task.FromResult(new ReadinessAuditData(
                ActiveEmployees: ActiveEmployees,
                EmployeesWithSalary: EmployeesWithSalary,
                EmployeesWithShift: EmployeesWithShift,
                EmployeesWithBank: EmployeesWithBank,
                EmployeesWithStatutory: EmployeesWithStatutory,
                ActivePostingProfilesCount: ActivePostingProfilesCount,
                ActiveAccountsCount: ActiveAccountsCount,
                PendingPolicyApprovalsCount: PendingPolicyApprovalsCount,
                ApprovedSalaryPolicyCount: ApprovedSalaryPolicyCount));
    }

    [Fact]
    public async Task Missing_salary_calculation_policy_is_blocking_even_when_salary_and_shift_exist()
    {
        var employee = Guid.NewGuid();
        var repo = new FakeReadinessRepository { ActiveEmployees = [new(employee, "EMP-POLICY", "Synthetic")],
            EmployeesWithSalary = [employee], EmployeesWithShift = [employee], ApprovedSalaryPolicyCount = 0 };
        var report = await new CompanyPayrollReadinessService(repo, TimeProvider.System).EvaluateAsync(2026, 10);
        Assert.False(report.IsReady);
        Assert.Contains(report.BlockingIssues, x => x.Code == "missing_salary_policy");
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(2026, 13)]
    [InlineData(9999, 12)]
    public async Task Invalid_period_is_a_validation_failure(int year, int month)
    {
        var error = await Assert.ThrowsAsync<Cosmix.Application.Authentication.AuthenticationFailure>(() =>
            new CompanyPayrollReadinessService(new FakeReadinessRepository(), TimeProvider.System).EvaluateAsync(year, month));
        Assert.Equal(400, error.StatusCode);
    }

    [Fact]
    public async Task Missing_salary_configuration_for_active_employee_is_detected_as_blocking_issue()
    {
        var empId = Guid.NewGuid();
        var repo = new FakeReadinessRepository
        {
            ActiveEmployees = [new(empId, "EMP-1001", "Ali Khan")],
            EmployeesWithSalary = [], // Missing salary
            EmployeesWithShift = [empId],
            EmployeesWithBank = [empId]
        };

        var service = new CompanyPayrollReadinessService(repo, TimeProvider.System);
        var report = await service.EvaluateAsync(2026, 10);

        Assert.False(report.IsReady);
        var issue = Assert.Single(report.BlockingIssues, x => x.Code == "missing_base_salary");
        Assert.Equal(empId, issue.EmployeeId);
        Assert.Equal("Ali Khan", issue.EmployeeName);
        Assert.True(issue.IsBlocking);
    }

    [Fact]
    public async Task Missing_shift_assignment_for_active_employee_is_detected_as_blocking_issue()
    {
        var empId = Guid.NewGuid();
        var repo = new FakeReadinessRepository
        {
            ActiveEmployees = [new(empId, "EMP-1002", "Bilal Ahmed")],
            EmployeesWithSalary = [empId],
            EmployeesWithShift = [], // Missing shift
            EmployeesWithBank = [empId]
        };

        var service = new CompanyPayrollReadinessService(repo, TimeProvider.System);
        var report = await service.EvaluateAsync(2026, 10);

        Assert.False(report.IsReady);
        var issue = Assert.Single(report.BlockingIssues, x => x.Code == "missing_shift_assignment");
        Assert.Equal(empId, issue.EmployeeId);
        Assert.True(issue.IsBlocking);
    }

    [Fact]
    public async Task Missing_posting_profile_is_detected_as_blocking_issue()
    {
        var empId = Guid.NewGuid();
        var repo = new FakeReadinessRepository
        {
            ActiveEmployees = [new(empId, "EMP-1003", "Zain Abbas")],
            EmployeesWithSalary = [empId],
            EmployeesWithShift = [empId],
            ActivePostingProfilesCount = 0 // Missing profile
        };

        var service = new CompanyPayrollReadinessService(repo, TimeProvider.System);
        var report = await service.EvaluateAsync(2026, 10);

        Assert.False(report.IsReady);
        Assert.Contains(report.BlockingIssues, x => x.Code == "missing_payroll_posting_profile");
    }

    [Fact]
    public async Task Missing_bank_details_is_flagged_as_warning()
    {
        var empId = Guid.NewGuid();
        var repo = new FakeReadinessRepository
        {
            ActiveEmployees = [new(empId, "EMP-1004", "Hamza Tariq")],
            EmployeesWithSalary = [empId],
            EmployeesWithShift = [empId],
            EmployeesWithBank = [] // Missing bank details
        };

        var service = new CompanyPayrollReadinessService(repo, TimeProvider.System);
        var report = await service.EvaluateAsync(2026, 10);

        var warning = Assert.Single(report.Warnings, x => x.Code == "missing_bank_account");
        Assert.Equal(empId, warning.EmployeeId);
        Assert.False(warning.IsBlocking);
    }

    [Fact]
    public async Task Unapproved_policy_version_is_flagged_as_warning()
    {
        var empId = Guid.NewGuid();
        var repo = new FakeReadinessRepository
        {
            ActiveEmployees = [new(empId, "EMP-1005", "Usman Ghani")],
            EmployeesWithSalary = [empId],
            EmployeesWithShift = [empId],
            EmployeesWithBank = [empId],
            PendingPolicyApprovalsCount = 2 // 2 draft policies
        };

        var service = new CompanyPayrollReadinessService(repo, TimeProvider.System);
        var report = await service.EvaluateAsync(2026, 10);

        var warning = Assert.Single(report.Warnings, x => x.Code == "unapproved_policy_version_pending");
        Assert.Contains("2 policy version(s)", warning.Message);
        Assert.False(warning.IsBlocking);
    }

    [Fact]
    public async Task Fully_configured_company_returns_ready_status_with_zero_blocking_issues()
    {
        var empId = Guid.NewGuid();
        var repo = new FakeReadinessRepository
        {
            ActiveEmployees = [new(empId, "EMP-READY", "Rashid Minhas")],
            EmployeesWithSalary = [empId],
            EmployeesWithShift = [empId],
            EmployeesWithBank = [empId],
            EmployeesWithStatutory = [empId],
            ActivePostingProfilesCount = 1,
            ActiveAccountsCount = 10,
            PendingPolicyApprovalsCount = 0
        };

        var service = new CompanyPayrollReadinessService(repo, TimeProvider.System);
        var report = await service.EvaluateAsync(2026, 10);

        Assert.True(report.IsReady);
        Assert.Empty(report.BlockingIssues);
        Assert.Equal(1, report.TotalActiveEmployees);
        Assert.Equal(1, report.ReadyEmployeesCount);
        Assert.Equal(1, report.Coverage.EmployeesWithSalaryCount);
        Assert.Equal(1, report.Coverage.EmployeesWithShiftCount);
        Assert.Equal(1, report.Coverage.EmployeesWithBankCount);
        Assert.Equal(1, report.Coverage.ActivePostingProfilesCount);
    }

    [Fact]
    public void Readiness_report_never_leaks_confidential_salary_amounts()
    {
        var reportProps = typeof(CompanyPayrollReadinessReport).GetProperties();
        Assert.DoesNotContain(reportProps, p => p.PropertyType == typeof(decimal) || p.PropertyType == typeof(decimal?));
        Assert.DoesNotContain(reportProps, p => p.Name.Contains("Amount", StringComparison.OrdinalIgnoreCase) || p.Name.Contains("SalaryValue", StringComparison.OrdinalIgnoreCase));

        var issueProps = typeof(PayrollReadinessIssue).GetProperties();
        Assert.DoesNotContain(issueProps, p => p.PropertyType == typeof(decimal) || p.PropertyType == typeof(decimal?));
        Assert.DoesNotContain(issueProps, p => p.Name.Contains("Amount", StringComparison.OrdinalIgnoreCase));
    }

    [PayrollSqlFact]
    [Trait("Category", "SqlIntegration")]
    public async Task SQL_persistence_company_readiness_queries_translate_and_execute_against_localdb()
    {
        var connection = Environment.GetEnvironmentVariable("COSMIX_PAYROLL_VERIFICATION_CONNECTION")
            ?? throw new InvalidOperationException("Disposable SQL target required.");
        var options = new DbContextOptionsBuilder<CosmixDbContext>().UseSqlServer(connection).Options;
        await using var db = new CosmixDbContext(options);

        var repo = new CompanyPayrollReadinessRepository(db, new EmployeeCompensationRepository(db), new UnconfiguredRoster(),
            new Cosmix.Application.HR.PayrollAccounting.PayrollFinanceSetupService(new PayrollFinanceRepository(db)));
        var data = await repo.FetchReadinessDataAsync(new DateOnly(2026, 10, 1), default);

        Assert.NotNull(data);
        Assert.NotNull(data.ActiveEmployees);
        Assert.NotNull(data.EmployeesWithSalary);
        Assert.NotNull(data.EmployeesWithShift);
        Assert.NotNull(data.EmployeesWithBank);
        Assert.NotNull(data.EmployeesWithStatutory);
        Assert.True(data.ActivePostingProfilesCount >= 0);
        Assert.True(data.ActiveAccountsCount >= 0);
    }

    // This integration case verifies SQL translation. Runtime roster decisions are exercised
    // separately by PayrollReadinessCoverageTests and the roster acceptance suites.
    private sealed class UnconfiguredRoster : Cosmix.Application.Attendance.Rosters.IIndividualRosterResolver
    {
        public Task<Cosmix.Application.Attendance.Rosters.EffectiveRosterResponse> ResolveAsync(
            Guid employeeId, DateOnly businessDate, CancellationToken ct) =>
            Task.FromResult(new Cosmix.Application.Attendance.Rosters.EffectiveRosterResponse(
                employeeId, businessDate, "not_configured", "Asia/Karachi"));
    }
}
