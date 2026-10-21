namespace NOIR.Application.Features.Dashboard.Queries.GetBlogDashboard;

public class GetBlogDashboardQueryHandler
{
    private readonly IBlogDashboardQueryService _blogDashboardService;

    public GetBlogDashboardQueryHandler(IBlogDashboardQueryService blogDashboardService)
    {
        _blogDashboardService = blogDashboardService;
    }

    public async Task<Result<BlogDashboardDto>> Handle(
        GetBlogDashboardQuery query,
        CancellationToken cancellationToken)
    {
        var result = await _blogDashboardService.GetBlogDashboardAsync(
            query.TrendDays,
            cancellationToken);

        return Result.Success(result);
    }
}
