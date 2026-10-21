namespace NOIR.Application.Features.Dashboard;

/// <summary>
/// Service for aggregating blog dashboard data (post counts, top posts, publishing trends).
/// Implemented in Infrastructure for direct DbContext access.
/// </summary>
public interface IBlogDashboardQueryService
{
    Task<DTOs.BlogDashboardDto> GetBlogDashboardAsync(
        int trendDays,
        CancellationToken cancellationToken = default);
}
