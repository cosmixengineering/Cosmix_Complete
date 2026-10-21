namespace NOIR.Application.Features.Pm.Queries.GetTasks;

public sealed record GetTasksQuery(
    Guid ProjectId,
    ProjectTaskStatus? Status = null,
    TaskPriority? Priority = null,
    Guid? AssigneeId = null,
    string? Search = null,
    int Page = 1,
    int PageSize = 20);
