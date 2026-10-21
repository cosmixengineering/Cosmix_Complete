namespace NOIR.Application.Features.Crm.Queries.GetActivities;

public sealed record GetActivitiesQuery(
    Guid? ContactId = null,
    Guid? LeadId = null,
    int Page = 1,
    int PageSize = 20);
