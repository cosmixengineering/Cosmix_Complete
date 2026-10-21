namespace NOIR.Application.Features.Pm.Queries.GetProjects;

public sealed record GetProjectsQuery(
    string? Search = null,
    ProjectStatus? Status = null,
    Guid? OwnerId = null,
    int Page = 1,
    int PageSize = 20,
    string? OrderBy = null,
    bool IsDescending = true);
