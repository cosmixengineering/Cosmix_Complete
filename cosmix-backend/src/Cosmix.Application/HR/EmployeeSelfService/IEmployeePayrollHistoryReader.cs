using Cosmix.Application.Common.Models;

namespace Cosmix.Application.HR.EmployeeSelfService;

public interface IEmployeePayrollHistoryReader
{
    Task<PagedResult<EssPayrollHandoffSummary>> GetPayrollHistoryAsync(Guid employeeId, int page, int pageSize, CancellationToken ct);
}
