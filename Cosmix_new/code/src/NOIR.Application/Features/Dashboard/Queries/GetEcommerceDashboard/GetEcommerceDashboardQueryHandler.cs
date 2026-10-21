namespace NOIR.Application.Features.Dashboard.Queries.GetEcommerceDashboard;

public class GetEcommerceDashboardQueryHandler
{
    private readonly IDashboardQueryService _dashboardService;

    public GetEcommerceDashboardQueryHandler(IDashboardQueryService dashboardService)
    {
        _dashboardService = dashboardService;
    }

    public async Task<Result<DashboardMetricsDto>> Handle(
        GetEcommerceDashboardQuery query,
        CancellationToken cancellationToken)
    {
        var metrics = await _dashboardService.GetMetricsAsync(
            query.TopProductsCount,
            query.LowStockThreshold,
            query.RecentOrdersCount,
            query.SalesOverTimeDays,
            cancellationToken);

        return Result.Success(metrics);
    }
}
