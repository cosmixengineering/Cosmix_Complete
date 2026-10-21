using System.ComponentModel.DataAnnotations;
using Cosmix.Application.Common.Exceptions;
using Cosmix.Application.Common.Models;

namespace Cosmix.Application.HR.Departments;

public sealed class DepartmentService(IDepartmentRepository repository) : IDepartmentService
{
    public async Task<PagedResult<DepartmentResponse>> ListAsync(
        DepartmentListRequest request, CancellationToken cancellationToken)
    {
        if (!Validator.TryValidateObject(request, new ValidationContext(request), [], true))
            throw new RequestValidationException("Invalid department filters or pagination.");

        var result = await repository.ListAsync(request, cancellationToken);
        return new(result.Items.Select(x => new DepartmentResponse(x.Id, x.Code, x.Name, x.IsActive)).ToArray(),
            result.TotalCount, result.Page, result.PageSize);
    }

    public async Task<DepartmentResponse> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        var department = await repository.GetByIdAsync(id, cancellationToken)
            ?? throw new ResourceNotFoundException("Department was not found.");
        return new(department.Id, department.Code, department.Name, department.IsActive);
    }
}
