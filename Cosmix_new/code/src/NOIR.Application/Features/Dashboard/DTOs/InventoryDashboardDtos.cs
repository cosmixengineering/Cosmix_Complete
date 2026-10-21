namespace NOIR.Application.Features.Dashboard.DTOs;

public sealed record InventoryDashboardDto(
    IReadOnlyList<LowStockAlertDto> LowStockAlerts,
    IReadOnlyList<RecentReceiptDto> RecentReceipts,
    InventoryValueSummaryDto ValueSummary,
    IReadOnlyList<StockMovementTrendDto> StockMovementTrend);

public sealed record LowStockAlertDto(Guid ProductId, string ProductName, string? Sku, int CurrentStock, int Threshold);
public sealed record RecentReceiptDto(Guid ReceiptId, string ReceiptNumber, string Type, DateTimeOffset Date, int ItemCount);
public sealed record InventoryValueSummaryDto(decimal TotalValue, int TotalSku, int InStockSku, int OutOfStockSku);
public sealed record StockMovementTrendDto(DateOnly Date, int StockIn, int StockOut);
