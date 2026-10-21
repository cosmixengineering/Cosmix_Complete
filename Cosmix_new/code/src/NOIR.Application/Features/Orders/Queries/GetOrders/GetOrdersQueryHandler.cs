namespace NOIR.Application.Features.Orders.Queries.GetOrders;

/// <summary>
/// Wolverine handler for getting orders with pagination.
/// </summary>
public class GetOrdersQueryHandler
{
    private readonly IRepository<Order, Guid> _orderRepository;
    private readonly IUserDisplayNameService _userDisplayNameService;

    public GetOrdersQueryHandler(IRepository<Order, Guid> orderRepository, IUserDisplayNameService userDisplayNameService)
    {
        _orderRepository = orderRepository;
        _userDisplayNameService = userDisplayNameService;
    }

    public async Task<Result<PagedResult<OrderSummaryDto>>> Handle(
        GetOrdersQuery query,
        CancellationToken cancellationToken)
    {
        var skip = (query.Page - 1) * query.PageSize;

        // Get total count
        var countSpec = new OrdersCountSpec(
            query.Status,
            query.CustomerEmail,
            query.FromDate,
            query.ToDate);
        var totalCount = await _orderRepository.CountAsync(countSpec, cancellationToken);

        // Get orders
        var listSpec = new OrdersListSpec(
            skip,
            query.PageSize,
            query.Status,
            query.CustomerEmail,
            query.FromDate,
            query.ToDate,
            query.OrderBy,
            query.IsDescending);
        var orders = await _orderRepository.ListAsync(listSpec, cancellationToken);

        // Resolve user names
        var userIds = orders
            .SelectMany(x => new[] { x.CreatedBy, x.ModifiedBy })
            .Where(id => !string.IsNullOrEmpty(id))
            .Select(id => id!)
            .Distinct();
        var userNames = await _userDisplayNameService.GetDisplayNamesAsync(userIds, cancellationToken);

        var items = orders.Select(o => OrderMapper.ToSummaryDto(o, userNames)).ToList();

        // PagedResult expects pageIndex (0-based), but query.Page is 1-based
        var pageIndex = query.Page - 1;
        return Result.Success(PagedResult<OrderSummaryDto>.Create(
            items,
            totalCount,
            pageIndex,
            query.PageSize));
    }
}
