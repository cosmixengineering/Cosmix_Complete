namespace NOIR.Application.Features.Dashboard.DTOs;

public sealed record CoreDashboardDto(
    QuickActionCountsDto QuickActions,
    IReadOnlyList<ActivityFeedItemDto> RecentActivity,
    SystemHealthDto? SystemHealth);

public sealed record QuickActionCountsDto(
    int PendingOrders,
    int PendingReviews,
    int LowStockAlerts,
    int DraftProducts);

public sealed record ActivityFeedItemDto(
    string Type,
    string Title,
    string Description,
    DateTimeOffset Timestamp,
    string? EntityId,
    string? EntityUrl,
    string? UserEmail,
    string? TargetDisplayName);

public sealed record SystemHealthDto(
    bool ApiHealthy,
    int BackgroundJobsQueued,
    int BackgroundJobsFailed,
    int ActiveTenants);
