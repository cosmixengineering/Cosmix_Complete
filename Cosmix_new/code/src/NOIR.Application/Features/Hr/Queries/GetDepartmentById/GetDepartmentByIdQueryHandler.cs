namespace NOIR.Application.Features.Hr.Queries.GetDepartmentById;

public class GetDepartmentByIdQueryHandler
{
    private readonly IRepository<Department, Guid> _departmentRepository;
    private readonly IRepository<Employee, Guid> _employeeRepository;

    public GetDepartmentByIdQueryHandler(
        IRepository<Department, Guid> departmentRepository,
        IRepository<Employee, Guid> employeeRepository)
    {
        _departmentRepository = departmentRepository;
        _employeeRepository = employeeRepository;
    }

    public async Task<Result<Features.Hr.DTOs.DepartmentDto>> Handle(
        GetDepartmentByIdQuery query,
        CancellationToken cancellationToken)
    {
        var spec = new Specifications.DepartmentByIdReadOnlySpec(query.Id);
        var department = await _departmentRepository.FirstOrDefaultAsync(spec, cancellationToken);

        if (department is null)
        {
            return Result.Failure<Features.Hr.DTOs.DepartmentDto>(
                Error.NotFound($"Department with ID '{query.Id}' not found.", "NOIR-HR-020"));
        }

        var employeeCountSpec = new Specifications.EmployeesByDepartmentSpec(department.Id);
        var employeeCount = await _employeeRepository.CountAsync(employeeCountSpec, cancellationToken);

        var subDepartments = department.SubDepartments
            .OrderBy(s => s.SortOrder)
            .ThenBy(s => s.Name)
            .Select(s => new Features.Hr.DTOs.DepartmentTreeNodeDto(
                s.Id, s.Name, s.Code, null, 0, s.IsActive,
                new List<Features.Hr.DTOs.DepartmentTreeNodeDto>()))
            .ToList();

        return Result.Success(new Features.Hr.DTOs.DepartmentDto(
            department.Id, department.Name, department.Code, department.Description,
            department.ManagerId,
            department.Manager != null ? $"{department.Manager.FirstName} {department.Manager.LastName}" : null,
            department.ParentDepartmentId,
            department.ParentDepartment?.Name,
            department.SortOrder, department.IsActive,
            employeeCount, subDepartments));
    }
}
