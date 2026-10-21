namespace NOIR.Application.Features.Hr.Queries.SearchEmployees;

public class SearchEmployeesQueryHandler
{
    private readonly IRepository<Employee, Guid> _employeeRepository;

    public SearchEmployeesQueryHandler(IRepository<Employee, Guid> employeeRepository)
    {
        _employeeRepository = employeeRepository;
    }

    public async Task<Result<List<Features.Hr.DTOs.EmployeeSearchDto>>> Handle(
        SearchEmployeesQuery query,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(query.SearchText))
        {
            return Result.Success(new List<Features.Hr.DTOs.EmployeeSearchDto>());
        }

        var spec = new Specifications.EmployeeSearchSpec(query.SearchText, query.Take);
        var employees = await _employeeRepository.ListAsync(spec, cancellationToken);

        var results = employees.Select(e => new Features.Hr.DTOs.EmployeeSearchDto(
            e.Id, e.EmployeeCode, $"{e.FirstName} {e.LastName}",
            e.AvatarUrl, e.Position, e.Department?.Name ?? "")).ToList();

        return Result.Success(results);
    }
}
