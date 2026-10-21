namespace NOIR.Application.Features.Dashboard.Queries.GetCoreDashboard;

public class GetCoreDashboardQueryHandler
{
    private readonly ICoreDashboardQueryService _coreDashboardService;
    private readonly ICurrentUser _currentUser;

    public GetCoreDashboardQueryHandler(
        ICoreDashboardQueryService coreDashboardService,
        ICurrentUser currentUser)
    {
        _coreDashboardService = coreDashboardService;
        _currentUser = currentUser;
    }

    public async Task<Result<DTOs.CoreDashboardDto>> Handle(
        GetCoreDashboardQuery query,
        CancellationToken cancellationToken)
    {
        var result = await _coreDashboardService.GetCoreDashboardAsync(
            query.ActivityCount,
            _currentUser.IsPlatformAdmin,
            cancellationToken);

        return Result.Success(result);
    }
}
