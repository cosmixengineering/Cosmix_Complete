using NOIR.Application.Common.Models;

namespace NOIR.Application.Features.Hr.Queries.GetEmployees;

public class GetEmployeesQueryHandler
{
    private readonly IRepository<Employee, Guid> _employeeRepository;
    private readonly IUserDisplayNameService _userDisplayNameService;

    public GetEmployeesQueryHandler(IRepository<Employee, Guid> employeeRepository, IUserDisplayNameService userDisplayNameService)
    {
        _employeeRepository = employeeRepository;
        _userDisplayNameService = userDisplayNameService;
    }

    public async Task<Result<PagedResult<Features.Hr.DTOs.EmployeeListDto>>> Handle(
        GetEmployeesQuery query,
        CancellationToken cancellationToken)
    {
        var skip = (query.Page - 1) * query.PageSize;

        var spec = new Specifications.EmployeesFilterSpec(
            query.Search, query.DepartmentId, query.Status, query.EmploymentType,
            skip, query.PageSize, query.OrderBy, query.IsDescending);

        var employees = await _employeeRepository.ListAsync(spec, cancellationToken);

        var countSpec = new Specifications.EmployeesCountSpec(
            query.Search, query.DepartmentId, query.Status, query.EmploymentType);
        var totalCount = await _employeeRepository.CountAsync(countSpec, cancellationToken);

        // Resolve user names
        var userIds = employees
            .SelectMany(x => new[] { x.CreatedBy, x.ModifiedBy })
            .Where(id => !string.IsNullOrEmpty(id))
            .Select(id => id!)
            .Distinct();
        var userNames = await _userDisplayNameService.GetDisplayNamesAsync(userIds, cancellationToken);

        var items = employees.Select(e => new Features.Hr.DTOs.EmployeeListDto(
            e.Id, e.EmployeeCode, e.FirstName, e.LastName, e.Email, e.AvatarUrl,
            e.Department?.Name ?? "", e.Position,
            e.Manager != null ? $"{e.Manager.FirstName} {e.Manager.LastName}" : null,
            e.Status, e.EmploymentType, e.CreatedAt, e.ModifiedAt,
            e.CreatedBy != null ? userNames.GetValueOrDefault(e.CreatedBy) : null,
            e.ModifiedBy != null ? userNames.GetValueOrDefault(e.ModifiedBy) : null)).ToList();

        return Result.Success(PagedResult<Features.Hr.DTOs.EmployeeListDto>.Create(
            items, totalCount, query.Page - 1, query.PageSize));
    }
}
