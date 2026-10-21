namespace NOIR.Application.Features.Dashboard.Queries.GetInventoryDashboard;

public class GetInventoryDashboardQueryHandler
{
    private readonly IInventoryDashboardQueryService _inventoryDashboardService;

    public GetInventoryDashboardQueryHandler(IInventoryDashboardQueryService inventoryDashboardService)
    {
        _inventoryDashboardService = inventoryDashboardService;
    }

    public async Task<Result<InventoryDashboardDto>> Handle(
        GetInventoryDashboardQuery query,
        CancellationToken cancellationToken)
    {
        var result = await _inventoryDashboardService.GetInventoryDashboardAsync(
            query.LowStockThreshold,
            query.RecentReceiptsCount,
            cancellationToken);

        return Result.Success(result);
    }
}
