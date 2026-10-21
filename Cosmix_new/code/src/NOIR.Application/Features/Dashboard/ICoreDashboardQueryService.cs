namespace NOIR.Application.Features.Dashboard;

/// <summary>
/// Service for aggregating core dashboard data (quick actions, activity feed, system health).
/// Implemented in Infrastructure for direct DbContext access.
/// </summary>
public interface ICoreDashboardQueryService
{
    Task<DTOs.CoreDashboardDto> GetCoreDashboardAsync(
        int activityCount,
        bool includeSysHealth,
        CancellationToken cancellationToken = default);
}
