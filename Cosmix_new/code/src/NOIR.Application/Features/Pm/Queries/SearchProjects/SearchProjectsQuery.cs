namespace NOIR.Application.Features.Pm.Queries.SearchProjects;

public sealed record SearchProjectsQuery(string SearchText, int Take = 10);
