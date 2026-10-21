namespace NOIR.Infrastructure.Services;

/// <summary>
/// Infrastructure implementation of blog dashboard query service.
/// Uses direct DbContext access for efficient aggregation queries.
/// </summary>
public class BlogDashboardQueryService : IBlogDashboardQueryService, IScopedService
{
    private readonly ApplicationDbContext _context;

    public BlogDashboardQueryService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<BlogDashboardDto> GetBlogDashboardAsync(
        int trendDays,
        CancellationToken cancellationToken = default)
    {
        var trendStartDate = DateTimeOffset.UtcNow.AddDays(-trendDays);

        // DbContext is not thread-safe — run queries sequentially
        var statusCounts = await _context.Posts
            .TagWith("Dashboard_Blog_StatusCounts")
            .GroupBy(p => p.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        var statusDict = statusCounts.ToDictionary(x => x.Status, x => x.Count);
        var totalPosts = statusDict.Values.Sum();
        var publishedPosts = statusDict.GetValueOrDefault(PostStatus.Published);
        var draftPosts = statusDict.GetValueOrDefault(PostStatus.Draft);
        var archivedPosts = statusDict.GetValueOrDefault(PostStatus.Archived);

        var topPosts = await _context.Posts
            .TagWith("Dashboard_Blog_TopPosts")
            .OrderByDescending(p => p.ViewCount)
            .Take(5)
            .Select(p => new TopPostDto(p.Id, p.Title, p.FeaturedImageUrl, p.ViewCount))
            .ToListAsync(cancellationToken);

        // Materialize first, then group in memory to avoid GroupBy(date) translation issues
        var rawTrend = await _context.Posts
            .TagWith("Dashboard_Blog_PublishingTrend")
            .Where(p => p.CreatedAt >= trendStartDate)
            .Select(p => new { p.CreatedAt })
            .ToListAsync(cancellationToken);

        var publishingTrend = rawTrend
            .GroupBy(p => DateOnly.FromDateTime(p.CreatedAt.Date))
            .Select(g => new PublishingTrendDto(g.Key, g.Count()))
            .OrderBy(x => x.Date)
            .ToList();

        return new BlogDashboardDto(
            totalPosts,
            publishedPosts,
            draftPosts,
            archivedPosts,
            PendingComments: 0, // No comment moderation exists yet
            topPosts,
            publishingTrend);
    }
}
