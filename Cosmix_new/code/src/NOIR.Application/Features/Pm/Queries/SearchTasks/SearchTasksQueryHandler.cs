namespace NOIR.Application.Features.Pm.Queries.SearchTasks;

public class SearchTasksQueryHandler
{
    private readonly IApplicationDbContext _dbContext;

    public SearchTasksQueryHandler(IApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Result<List<Features.Pm.DTOs.TaskSearchDto>>> Handle(
        SearchTasksQuery query,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(query.SearchText))
        {
            return Result.Success(new List<Features.Pm.DTOs.TaskSearchDto>());
        }

        var searchText = query.SearchText.Trim().ToLowerInvariant();

        var tasks = await _dbContext.ProjectTasks
            .Where(t => t.ProjectId == query.ProjectId &&
                (t.Title.ToLower().Contains(searchText) || t.TaskNumber.ToLower().Contains(searchText)))
            .OrderByDescending(t => t.CreatedAt)
            .Take(query.Take)
            .TagWith("SearchTasks")
            .Select(t => new Features.Pm.DTOs.TaskSearchDto(t.Id, t.TaskNumber, t.Title, t.Status, t.Priority))
            .ToListAsync(cancellationToken);

        return Result.Success(tasks);
    }
}
