namespace Cosmix.Application.HR.PayrollSalary;

public sealed record ActiveEmployeeReadinessDto(Guid Id, string EmployeeCode, string FullLegalName,
    DateOnly? JoinedOn = null);

public sealed record ReadinessAuditData(
    IReadOnlyList<ActiveEmployeeReadinessDto> ActiveEmployees,
    IReadOnlySet<Guid> EmployeesWithSalary,
    IReadOnlySet<Guid> EmployeesWithShift,
    IReadOnlySet<Guid> EmployeesWithBank,
    IReadOnlySet<Guid> EmployeesWithStatutory,
    int ActivePostingProfilesCount,
    int ActiveAccountsCount,
    int PendingPolicyApprovalsCount,
    int ApprovedSalaryPolicyCount = 0);

public interface ICompanyPayrollReadinessRepository
{
    Task<ReadinessAuditData> FetchReadinessDataAsync(DateOnly targetMonth, CancellationToken ct);
}
