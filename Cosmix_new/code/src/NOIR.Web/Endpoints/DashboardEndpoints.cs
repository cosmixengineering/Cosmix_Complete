namespace NOIR.Web.Endpoints;

/// <summary>
/// Dashboard API endpoints.
/// Provides aggregated metrics for the admin dashboard.
/// </summary>
public static class DashboardEndpoints
{
    public static void MapDashboardEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/dashboard")
            .WithTags("Dashboard")
            .RequireAuthorization();

        // Get dashboard metrics
        group.MapGet("/metrics", async (
            [FromQuery] int? topProducts,
            [FromQuery] int? lowStockThreshold,
            [FromQuery] int? recentOrders,
            [FromQuery] int? salesDays,
            IMessageBus bus) =>
        {
            var query = new GetDashboardMetricsQuery(
                topProducts ?? 5,
                lowStockThreshold ?? 10,
                recentOrders ?? 10,
                salesDays ?? 30);
            var result = await bus.InvokeAsync<Result<DashboardMetricsDto>>(query);
            return result.ToHttpResult();
        })
        // Dashboard aggregates data across orders, products, and inventory.
        // Uses OrdersRead as the minimum required permission since revenue
        // and order metrics are the primary dashboard content.
        .RequireAuthorization(Permissions.OrdersRead)
        .WithName("GetDashboardMetrics")
        .WithSummary("Get dashboard metrics")
        .WithDescription("Returns aggregated dashboard metrics including revenue, order counts, top products, and more.")
        .Produces<DashboardMetricsDto>(StatusCodes.Status200OK);

        // GET /api/dashboard/core
        group.MapGet("/core", async (
            [FromQuery] int? activityCount,
            IMessageBus bus) =>
        {
            var result = await bus.InvokeAsync<Result<CoreDashboardDto>>(
                new GetCoreDashboardQuery(activityCount ?? 10));
            return result.ToHttpResult();
        })
        .RequireAuthorization(Permissions.DashboardRead)
        .WithName("GetCoreDashboard")
        .WithSummary("Get core dashboard data")
        .Produces<CoreDashboardDto>(StatusCodes.Status200OK);

        // GET /api/dashboard/ecommerce
        group.MapGet("/ecommerce", async (
            [FromQuery] int? topProducts,
            [FromQuery] int? lowStockThreshold,
            [FromQuery] int? recentOrders,
            [FromQuery] int? salesDays,
            IMessageBus bus) =>
        {
            var result = await bus.InvokeAsync<Result<DashboardMetricsDto>>(
                new GetEcommerceDashboardQuery(
                    topProducts ?? 5, lowStockThreshold ?? 10,
                    recentOrders ?? 10, salesDays ?? 30));
            return result.ToHttpResult();
        })
        .RequireAuthorization(Permissions.DashboardRead)
        .RequireFeature(ModuleNames.Ecommerce.Orders)
        .WithName("GetEcommerceDashboard")
        .WithSummary("Get ecommerce dashboard metrics")
        .Produces<DashboardMetricsDto>(StatusCodes.Status200OK);

        // GET /api/dashboard/blog
        group.MapGet("/blog", async (
            [FromQuery] int? trendDays,
            IMessageBus bus) =>
        {
            var result = await bus.InvokeAsync<Result<BlogDashboardDto>>(
                new GetBlogDashboardQuery(trendDays ?? 30));
            return result.ToHttpResult();
        })
        .RequireAuthorization(Permissions.DashboardRead)
        .RequireFeature(ModuleNames.Content.Blog)
        .WithName("GetBlogDashboard")
        .WithSummary("Get blog dashboard metrics")
        .Produces<BlogDashboardDto>(StatusCodes.Status200OK);

        // GET /api/dashboard/inventory
        group.MapGet("/inventory", async (
            [FromQuery] int? lowStockThreshold,
            [FromQuery] int? recentReceipts,
            IMessageBus bus) =>
        {
            var result = await bus.InvokeAsync<Result<InventoryDashboardDto>>(
                new GetInventoryDashboardQuery(
                    lowStockThreshold ?? 10, recentReceipts ?? 5));
            return result.ToHttpResult();
        })
        .RequireAuthorization(Permissions.DashboardRead)
        .RequireFeature(ModuleNames.Ecommerce.Inventory)
        .WithName("GetInventoryDashboard")
        .WithSummary("Get inventory dashboard metrics")
        .Produces<InventoryDashboardDto>(StatusCodes.Status200OK);
    }
}
