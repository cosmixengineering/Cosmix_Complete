namespace NOIR.Application.Features.Pm.Queries.SearchTasks;

public sealed record SearchTasksQuery(Guid ProjectId, string SearchText, int Take = 10);
