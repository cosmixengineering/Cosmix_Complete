namespace NOIR.Application.Features.Pm.Queries.GetProjects;

public class GetProjectsQueryHandler
{
    private readonly IRepository<Project, Guid> _projectRepository;
    private readonly IUserDisplayNameService _userDisplayNameService;

    public GetProjectsQueryHandler(IRepository<Project, Guid> projectRepository, IUserDisplayNameService userDisplayNameService)
    {
        _projectRepository = projectRepository;
        _userDisplayNameService = userDisplayNameService;
    }

    public async Task<Result<PagedResult<Features.Pm.DTOs.ProjectListDto>>> Handle(
        GetProjectsQuery query,
        CancellationToken cancellationToken)
    {
        var skip = (query.Page - 1) * query.PageSize;

        var spec = new Specifications.ProjectsByFilterSpec(
            query.Search, query.Status, query.OwnerId,
            skip, query.PageSize, query.OrderBy, query.IsDescending);

        var projects = await _projectRepository.ListAsync(spec, cancellationToken);

        var countSpec = new Specifications.ProjectsCountSpec(
            query.Search, query.Status, query.OwnerId);
        var totalCount = await _projectRepository.CountAsync(countSpec, cancellationToken);

        // Resolve user names
        var userIds = projects
            .SelectMany(x => new[] { x.CreatedBy, x.ModifiedBy })
            .Where(id => !string.IsNullOrEmpty(id))
            .Select(id => id!)
            .Distinct();
        var userNames = await _userDisplayNameService.GetDisplayNamesAsync(userIds, cancellationToken);

        var items = projects.Select(p =>
        {
            var taskCount = p.Tasks.Count;
            var completedTaskCount = p.Tasks.Count(t => t.Status == ProjectTaskStatus.Done);
            var progressPercent = taskCount > 0 ? Math.Round((decimal)completedTaskCount / taskCount * 100, 1) : 0;

            return new Features.Pm.DTOs.ProjectListDto(
                p.Id, p.Name, p.Slug, p.Status,
                p.StartDate, p.EndDate, p.DueDate,
                p.Owner != null ? $"{p.Owner.FirstName} {p.Owner.LastName}" : null,
                p.Members.Count, taskCount, completedTaskCount,
                p.Color, p.Icon, p.Visibility, p.CreatedAt, p.ModifiedAt,
                p.ProjectCode, p.OwnerId, p.Owner?.AvatarUrl, progressPercent,
                p.CreatedBy != null ? userNames.GetValueOrDefault(p.CreatedBy) : null,
                p.ModifiedBy != null ? userNames.GetValueOrDefault(p.ModifiedBy) : null);
        }).ToList();

        return Result.Success(PagedResult<Features.Pm.DTOs.ProjectListDto>.Create(
            items, totalCount, query.Page - 1, query.PageSize));
    }
}
