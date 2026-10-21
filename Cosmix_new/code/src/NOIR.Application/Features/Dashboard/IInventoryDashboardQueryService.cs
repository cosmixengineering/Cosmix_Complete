namespace NOIR.Application.Features.Dashboard;

/// <summary>
/// Service for aggregating inventory dashboard data (low stock, receipts, value summary, trends).
/// Implemented in Infrastructure for direct DbContext access.
/// </summary>
public interface IInventoryDashboardQueryService
{
    Task<DTOs.InventoryDashboardDto> GetInventoryDashboardAsync(
        int lowStockThreshold,
        int recentReceiptsCount,
        CancellationToken cancellationToken = default);
}
