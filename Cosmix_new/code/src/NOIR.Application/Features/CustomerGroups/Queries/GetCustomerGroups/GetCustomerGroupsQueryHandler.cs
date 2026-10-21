namespace NOIR.Application.Features.CustomerGroups.Queries.GetCustomerGroups;

/// <summary>
/// Wolverine handler for getting paged list of customer groups.
/// </summary>
public class GetCustomerGroupsQueryHandler
{
    private readonly IRepository<CustomerGroup, Guid> _repository;
    private readonly IUserDisplayNameService _userDisplayNameService;

    public GetCustomerGroupsQueryHandler(IRepository<CustomerGroup, Guid> repository, IUserDisplayNameService userDisplayNameService)
    {
        _repository = repository;
        _userDisplayNameService = userDisplayNameService;
    }

    public async Task<Result<PagedResult<CustomerGroupListDto>>> Handle(
        GetCustomerGroupsQuery query,
        CancellationToken cancellationToken)
    {
        // Get paged groups
        var spec = new CustomerGroupsPagedSpec(
            query.Search,
            query.IsActive,
            query.Page,
            query.PageSize,
            query.OrderBy,
            query.IsDescending);

        var groups = await _repository.ListAsync(spec, cancellationToken);

        // Get total count
        var countSpec = new CustomerGroupsCountSpec(
            query.Search,
            query.IsActive);

        var totalCount = await _repository.CountAsync(countSpec, cancellationToken);

        // Resolve user names
        var userIds = groups
            .SelectMany(x => new[] { x.CreatedBy, x.ModifiedBy })
            .Where(id => !string.IsNullOrEmpty(id))
            .Select(id => id!)
            .Distinct();
        var userNames = await _userDisplayNameService.GetDisplayNamesAsync(userIds, cancellationToken);

        var items = groups.Select(g => CustomerGroupMapper.ToListDto(g, userNames)).ToList();

        // PagedResult expects pageIndex (0-based), but query.Page is 1-based
        var pageIndex = query.Page - 1;

        return Result.Success(PagedResult<CustomerGroupListDto>.Create(
            items,
            totalCount,
            pageIndex,
            query.PageSize));
    }
}
