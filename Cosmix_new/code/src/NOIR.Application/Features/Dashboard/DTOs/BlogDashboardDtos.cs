namespace NOIR.Application.Features.Dashboard.DTOs;

public sealed record BlogDashboardDto(
    int TotalPosts,
    int PublishedPosts,
    int DraftPosts,
    int ArchivedPosts,
    int PendingComments,
    IReadOnlyList<TopPostDto> TopPosts,
    IReadOnlyList<PublishingTrendDto> PublishingTrend);

public sealed record TopPostDto(Guid PostId, string Title, string? ImageUrl, long ViewCount);
public sealed record PublishingTrendDto(DateOnly Date, int PostCount);
