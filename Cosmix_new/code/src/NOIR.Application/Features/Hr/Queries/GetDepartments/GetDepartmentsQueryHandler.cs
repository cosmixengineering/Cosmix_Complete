namespace NOIR.Application.Features.Hr.Queries.GetDepartments;

public class GetDepartmentsQueryHandler
{
    private readonly IRepository<Department, Guid> _departmentRepository;
    private readonly IRepository<Employee, Guid> _employeeRepository;

    public GetDepartmentsQueryHandler(
        IRepository<Department, Guid> departmentRepository,
        IRepository<Employee, Guid> employeeRepository)
    {
        _departmentRepository = departmentRepository;
        _employeeRepository = employeeRepository;
    }

    public async Task<Result<List<Features.Hr.DTOs.DepartmentTreeNodeDto>>> Handle(
        GetDepartmentsQuery query,
        CancellationToken cancellationToken)
    {
        var spec = new Specifications.AllDepartmentsSpec();
        var allDepartments = await _departmentRepository.ListAsync(spec, cancellationToken);

        if (!query.IncludeInactive)
        {
            allDepartments = allDepartments.Where(d => d.IsActive).ToList();
        }

        var rootDepartments = allDepartments.Where(d => d.ParentDepartmentId == null).ToList();
        var departmentLookup = allDepartments.ToLookup(d => d.ParentDepartmentId);

        var employeeCounts = new Dictionary<Guid, int>();
        foreach (var dept in allDepartments)
        {
            var countSpec = new Specifications.EmployeesByDepartmentSpec(dept.Id);
            var count = await _employeeRepository.CountAsync(countSpec, cancellationToken);
            employeeCounts[dept.Id] = count;
        }

        var tree = rootDepartments.Select(d => BuildTreeNode(d, departmentLookup, employeeCounts)).ToList();

        return Result.Success(tree);
    }

    private static Features.Hr.DTOs.DepartmentTreeNodeDto BuildTreeNode(
        Department department,
        ILookup<Guid?, Department> departmentLookup,
        Dictionary<Guid, int> employeeCounts)
    {
        var children = departmentLookup[department.Id]
            .Select(child => BuildTreeNode(child, departmentLookup, employeeCounts))
            .ToList();

        return new Features.Hr.DTOs.DepartmentTreeNodeDto(
            department.Id,
            department.Name,
            department.Code,
            department.Manager != null ? $"{department.Manager.FirstName} {department.Manager.LastName}" : null,
            employeeCounts.GetValueOrDefault(department.Id),
            department.IsActive,
            children);
    }
}
