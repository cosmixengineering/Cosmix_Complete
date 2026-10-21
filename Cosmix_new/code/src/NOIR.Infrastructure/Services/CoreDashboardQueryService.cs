namespace NOIR.Infrastructure.Services;

/// <summary>
/// Infrastructure implementation of core dashboard query service.
/// Uses direct DbContext access for efficient aggregation queries.
/// </summary>
public class CoreDashboardQueryService : ICoreDashboardQueryService, IScopedService
{
    private readonly ApplicationDbContext _context;

    public CoreDashboardQueryService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<CoreDashboardDto> GetCoreDashboardAsync(
        int activityCount,
        bool includeSysHealth,
        CancellationToken cancellationToken = default)
    {
        var quickActions = await GetQuickActionCountsAsync(cancellationToken);
        var recentActivity = await GetRecentActivityAsync(activityCount, cancellationToken);
        var systemHealth = includeSysHealth ? GetSystemHealth() : null;

        return new CoreDashboardDto(quickActions, recentActivity, systemHealth);
    }

    private async Task<QuickActionCountsDto> GetQuickActionCountsAsync(CancellationToken ct)
    {
        // DbContext is not thread-safe — run queries sequentially
        var pendingOrders = await _context.Set<Domain.Entities.Order.Order>()
            .TagWith("Dashboard_Core_PendingOrders")
            .CountAsync(o => o.Status == OrderStatus.Pending, ct);

        var pendingReviews = await _context.ProductReviews
            .TagWith("Dashboard_Core_PendingReviews")
            .CountAsync(r => r.Status == ReviewStatus.Pending, ct);

        var lowStockAlerts = await _context.ProductVariants
            .TagWith("Dashboard_Core_LowStockAlerts")
            .CountAsync(pv => pv.StockQuantity <= 10 && pv.StockQuantity >= 0, ct);

        var draftProducts = await _context.Products
            .TagWith("Dashboard_Core_DraftProducts")
            .CountAsync(p => p.Status == ProductStatus.Draft, ct);

        return new QuickActionCountsDto(pendingOrders, pendingReviews, lowStockAlerts, draftProducts);
    }

    private async Task<IReadOnlyList<ActivityFeedItemDto>> GetRecentActivityAsync(
        int count,
        CancellationToken ct)
    {
        var rawLogs = await _context.HandlerAuditLogs
            .TagWith("Dashboard_Core_RecentActivity")
            .Where(l => l.IsSuccess && l.ActionDescription != null)
            .OrderByDescending(l => l.StartTime)
            .Take(count)
            .Select(l => new
            {
                l.OperationType,
                l.PageContext,
                l.HandlerName,
                ActionDescription = l.ActionDescription!,
                l.StartTime,
                l.TargetDtoId,
                l.TargetDisplayName,
                UserEmail = l.HttpRequestAuditLog != null ? l.HttpRequestAuditLog.UserEmail : null
            })
            .ToListAsync(ct);

        return rawLogs.Select(l => new ActivityFeedItemDto(
            l.OperationType,
            l.PageContext ?? HumanizeHandlerName(l.HandlerName),
            l.ActionDescription,
            l.StartTime,
            l.TargetDtoId,
            null,
            l.UserEmail,
            l.TargetDisplayName)).ToList();
    }

    /// <summary>
    /// Converts "UpdateProductCommand" → "Product", "CreateOrderCommand" → "Order".
    /// Used as fallback when PageContext is not set.
    /// </summary>
    private static string HumanizeHandlerName(string handlerName)
    {
        var name = handlerName;

        // Strip common suffixes
        foreach (var suffix in new[] { "CommandHandler", "Command", "QueryHandler", "Query" })
        {
            if (name.EndsWith(suffix, StringComparison.Ordinal))
            {
                name = name[..^suffix.Length];
                break;
            }
        }

        // Strip operation prefixes
        foreach (var prefix in new[] { "Update", "Create", "Delete", "Get", "Set", "Toggle", "Activate", "Deactivate" })
        {
            if (name.StartsWith(prefix, StringComparison.Ordinal) && name.Length > prefix.Length)
            {
                name = name[prefix.Length..];
                break;
            }
        }

        // Insert spaces before uppercase letters: "ProductVariant" → "Product Variant"
        return System.Text.RegularExpressions.Regex.Replace(name, "(?<=[a-z])([A-Z])", " $1");
    }

    private static SystemHealthDto GetSystemHealth()
    {
        // Hardcoded healthy values for now — Hangfire stats not yet accessible here
        return new SystemHealthDto(
            ApiHealthy: true,
            BackgroundJobsQueued: 0,
            BackgroundJobsFailed: 0,
            ActiveTenants: 0);
    }
}
