namespace Cosmix.Application.HR.EmployeeSelfService;

public interface IEmployeeSelfServiceRepository
{
    Task<EssProfileResponse?> GetProfileAsync(Guid employeeId, CancellationToken ct);
    Task<EssLeaveRequestSelection> GetLeaveRequestIdsAsync(Guid employeeId, DateOnly today, CancellationToken ct);
    Task<EssFinancialOverviewResponse> GetFinancialOverviewAsync(Guid employeeId, CancellationToken ct);
    Task<EssRequestsSummaryResponse> GetRequestsSummaryAsync(Guid employeeId, CancellationToken ct);
}
