namespace NOIR.Application.Features.Manufacturing.Queries.GetProductionOrders;

/// <summary>
/// Wolverine handler for getting paged list of production orders.
/// </summary>
public class GetProductionOrdersQueryHandler
{
    private readonly IRepository<ProductionOrder, Guid> _orderRepository;

    public GetProductionOrdersQueryHandler(IRepository<ProductionOrder, Guid> orderRepository)
    {
        _orderRepository = orderRepository;
    }

    public async Task<Result<PagedResult<ProductionOrderListDto>>> Handle(
        GetProductionOrdersQuery query,
        CancellationToken cancellationToken)
    {
        var spec = new ProductionOrdersByFilterSpec(
            query.Search,
            query.Status,
            query.Page,
            query.PageSize);

        var orders = await _orderRepository.ListAsync(spec, cancellationToken);

        var countSpec = new ProductionOrdersCountSpec(query.Search, query.Status);
        var totalCount = await _orderRepository.CountAsync(countSpec, cancellationToken);

        var items = orders.Select(ManufacturingMapper.ToListDto).ToList();

        return Result.Success(PagedResult<ProductionOrderListDto>.Create(
            items,
            totalCount,
            query.Page - 1,
            query.PageSize));
    }
}
