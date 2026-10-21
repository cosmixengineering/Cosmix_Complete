using Cosmix.Application.Common.Models;
using Cosmix.Domain.HR.Departments;

namespace Cosmix.Application.HR.Departments;

public interface IDepartmentRepository
{
    Task<PagedResult<Department>> ListAsync(DepartmentListRequest request, CancellationToken cancellationToken);
    Task<Department?> GetByIdAsync(Guid id, CancellationToken cancellationToken);
}
