using Cosmix.Application.Common.Models;

namespace Cosmix.Application.HR.Departments;

public interface IDepartmentService
{
    Task<PagedResult<DepartmentResponse>> ListAsync(DepartmentListRequest request, CancellationToken cancellationToken);
    Task<DepartmentResponse> GetByIdAsync(Guid id, CancellationToken cancellationToken);
}
