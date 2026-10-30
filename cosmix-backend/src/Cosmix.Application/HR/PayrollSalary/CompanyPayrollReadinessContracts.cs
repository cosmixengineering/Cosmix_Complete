namespace Cosmix.Application.HR.PayrollSalary;

public sealed record CompanyPayrollReadinessReport(
    bool IsReady,
    DateOnly EvaluationPeriod,
    DateTimeOffset EvaluatedAtUtc,
    int TotalActiveEmployees,
    int ReadyEmployeesCount,
    IReadOnlyList<PayrollReadinessIssue> BlockingIssues,
    IReadOnlyList<PayrollReadinessIssue> Warnings,
    PayrollConfigurationCoverageSummary Coverage);

public sealed record PayrollReadinessIssue(
    string Category,
    string Code,
    string Message,
    Guid? EmployeeId = null,
    string? EmployeeCode = null,
    string? EmployeeName = null,
    bool IsBlocking = true);

public sealed record PayrollConfigurationCoverageSummary(
    int EmployeesWithSalaryCount,
    int EmployeesWithShiftCount,
    int EmployeesWithBankCount,
    int EmployeesWithStatutoryCount,
    int ActivePostingProfilesCount,
    int ActiveCostCentersCount,
    int PendingPolicyApprovalsCount);
