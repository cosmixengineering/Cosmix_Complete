namespace NOIR.Application.Features.Hr.Queries.GetEmployees;

public sealed record GetEmployeesQuery(
    string? Search = null,
    Guid? DepartmentId = null,
    EmployeeStatus? Status = null,
    EmploymentType? EmploymentType = null,
    int Page = 1,
    int PageSize = 20,
    string? OrderBy = null,
    bool IsDescending = true);
