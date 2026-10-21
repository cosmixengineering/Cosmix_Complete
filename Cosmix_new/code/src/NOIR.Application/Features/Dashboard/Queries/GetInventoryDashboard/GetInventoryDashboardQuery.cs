namespace NOIR.Application.Features.Dashboard.Queries.GetInventoryDashboard;

public sealed record GetInventoryDashboardQuery(int LowStockThreshold = 10, int RecentReceiptsCount = 5);
