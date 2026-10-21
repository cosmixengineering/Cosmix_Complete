namespace NOIR.Application.Features.Customers.Queries.GetCustomers;

/// <summary>
/// Wolverine handler for getting customers with pagination.
/// </summary>
public class GetCustomersQueryHandler
{
    private readonly IRepository<Domain.Entities.Customer.Customer, Guid> _customerRepository;
    private readonly IUserDisplayNameService _userDisplayNameService;

    public GetCustomersQueryHandler(IRepository<Domain.Entities.Customer.Customer, Guid> customerRepository, IUserDisplayNameService userDisplayNameService)
    {
        _customerRepository = customerRepository;
        _userDisplayNameService = userDisplayNameService;
    }

    public async Task<Result<PagedResult<CustomerSummaryDto>>> Handle(
        GetCustomersQuery query,
        CancellationToken cancellationToken)
    {
        var skip = (query.Page - 1) * query.PageSize;

        // Get total count
        var countSpec = new CustomersCountSpec(
            query.Search,
            query.Segment,
            query.Tier,
            query.IsActive);
        var totalCount = await _customerRepository.CountAsync(countSpec, cancellationToken);

        // Get customers
        var listSpec = new CustomersFilterSpec(
            skip,
            query.PageSize,
            query.Search,
            query.Segment,
            query.Tier,
            query.IsActive,
            query.OrderBy,
            query.IsDescending);
        var customers = await _customerRepository.ListAsync(listSpec, cancellationToken);

        // Resolve user names
        var userIds = customers
            .SelectMany(x => new[] { x.CreatedBy, x.ModifiedBy })
            .Where(id => !string.IsNullOrEmpty(id))
            .Select(id => id!)
            .Distinct();
        var userNames = await _userDisplayNameService.GetDisplayNamesAsync(userIds, cancellationToken);

        var items = customers.Select(c => CustomerMapper.ToSummaryDto(c, userNames)).ToList();

        var pageIndex = query.Page - 1;
        return Result.Success(PagedResult<CustomerSummaryDto>.Create(
            items,
            totalCount,
            pageIndex,
            query.PageSize));
    }
}
