namespace NOIR.Infrastructure.Services;

/// <summary>
/// Infrastructure implementation of inventory dashboard query service.
/// Uses direct DbContext access for efficient aggregation queries.
/// </summary>
public class InventoryDashboardQueryService : IInventoryDashboardQueryService, IScopedService
{
    private readonly ApplicationDbContext _context;

    public InventoryDashboardQueryService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<InventoryDashboardDto> GetInventoryDashboardAsync(
        int lowStockThreshold,
        int recentReceiptsCount,
        CancellationToken cancellationToken = default)
    {
        // DbContext is not thread-safe — run queries sequentially
        var lowStockAlerts = await GetLowStockAlertsAsync(lowStockThreshold, cancellationToken);
        var recentReceipts = await GetRecentReceiptsAsync(recentReceiptsCount, cancellationToken);
        var valueSummary = await GetValueSummaryAsync(cancellationToken);
        var stockMovementTrend = await GetStockMovementTrendAsync(cancellationToken);

        return new InventoryDashboardDto(lowStockAlerts, recentReceipts, valueSummary, stockMovementTrend);
    }

    private async Task<IReadOnlyList<LowStockAlertDto>> GetLowStockAlertsAsync(
        int threshold,
        CancellationToken ct)
    {
        return await _context.ProductVariants
            .TagWith("Dashboard_Inventory_LowStockAlerts")
            .Where(pv => pv.StockQuantity <= threshold && pv.StockQuantity >= 0)
            .OrderBy(pv => pv.StockQuantity)
            .Take(20)
            .Select(pv => new LowStockAlertDto(
                pv.Product.Id,
                pv.Product.Name,
                pv.Sku,
                pv.StockQuantity,
                threshold))
            .ToListAsync(ct);
    }

    private async Task<IReadOnlyList<RecentReceiptDto>> GetRecentReceiptsAsync(
        int count,
        CancellationToken ct)
    {
        return await _context.InventoryReceipts
            .TagWith("Dashboard_Inventory_RecentReceipts")
            .Where(r => r.Status == InventoryReceiptStatus.Confirmed)
            .OrderByDescending(r => r.ConfirmedAt)
            .Take(count)
            .Select(r => new RecentReceiptDto(
                r.Id,
                r.ReceiptNumber,
                r.Type.ToString(),
                r.ConfirmedAt!.Value,
                r.Items.Count))
            .ToListAsync(ct);
    }

    private async Task<InventoryValueSummaryDto> GetValueSummaryAsync(CancellationToken ct)
    {
        var variants = _context.ProductVariants;

        var totalSku = await variants
            .TagWith("Dashboard_Inventory_TotalSku")
            .CountAsync(ct);

        var inStockSku = await variants
            .TagWith("Dashboard_Inventory_InStockSku")
            .CountAsync(pv => pv.StockQuantity > 0, ct);

        var outOfStockSku = await variants
            .TagWith("Dashboard_Inventory_OutOfStockSku")
            .CountAsync(pv => pv.StockQuantity <= 0, ct);

        var totalValue = await variants
            .TagWith("Dashboard_Inventory_TotalValue")
            .SumAsync(pv => (decimal)pv.StockQuantity * pv.Price, ct);

        return new InventoryValueSummaryDto(totalValue, totalSku, inStockSku, outOfStockSku);
    }

    private async Task<IReadOnlyList<StockMovementTrendDto>> GetStockMovementTrendAsync(CancellationToken ct)
    {
        var trendStartDate = DateTimeOffset.UtcNow.AddDays(-30);

        // Materialize first, then group in memory to avoid GroupBy(date) translation issues
        var rawData = await _context.InventoryReceipts
            .TagWith("Dashboard_Inventory_StockMovementTrend")
            .Where(r => r.Status == InventoryReceiptStatus.Confirmed && r.ConfirmedAt >= trendStartDate)
            .Select(r => new { r.ConfirmedAt, r.Type, ItemCount = r.Items.Count })
            .ToListAsync(ct);

        return rawData
            .GroupBy(r => DateOnly.FromDateTime(r.ConfirmedAt!.Value.Date))
            .Select(g => new StockMovementTrendDto(
                g.Key,
                g.Where(r => r.Type == InventoryReceiptType.StockIn).Sum(r => r.ItemCount),
                g.Where(r => r.Type == InventoryReceiptType.StockOut).Sum(r => r.ItemCount)))
            .OrderBy(x => x.Date)
            .ToList();
    }
}
