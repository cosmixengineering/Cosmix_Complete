namespace NOIR.Application.Features.Dashboard.Queries.GetEcommerceDashboard;

public sealed record GetEcommerceDashboardQuery(
    int TopProductsCount = 5,
    int LowStockThreshold = 10,
    int RecentOrdersCount = 10,
    int SalesOverTimeDays = 30);
