namespace NOIR.Application.Features.Pm.Queries.GetProjectLabels;

public class GetProjectLabelsQueryHandler
{
    private readonly IApplicationDbContext _dbContext;

    public GetProjectLabelsQueryHandler(IApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Result<List<Features.Pm.DTOs.TaskLabelDto>>> Handle(
        GetProjectLabelsQuery query,
        CancellationToken cancellationToken)
    {
        var labels = await _dbContext.TaskLabels
            .Where(l => l.ProjectId == query.ProjectId)
            .OrderBy(l => l.Name)
            .TagWith("GetProjectLabels")
            .ToListAsync(cancellationToken);

        var items = labels.Select(l => new Features.Pm.DTOs.TaskLabelDto(
            l.Id, l.Name, l.Color)).ToList();

        return Result.Success(items);
    }
}
