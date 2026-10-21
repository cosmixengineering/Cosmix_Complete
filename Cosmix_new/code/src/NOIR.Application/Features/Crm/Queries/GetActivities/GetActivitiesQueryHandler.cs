namespace NOIR.Application.Features.Crm.Queries.GetActivities;

public class GetActivitiesQueryHandler
{
    private readonly IRepository<CrmActivity, Guid> _activityRepository;

    public GetActivitiesQueryHandler(IRepository<CrmActivity, Guid> activityRepository)
    {
        _activityRepository = activityRepository;
    }

    public async Task<Result<PagedResult<Features.Crm.DTOs.ActivityDto>>> Handle(
        GetActivitiesQuery query,
        CancellationToken cancellationToken)
    {
        var skip = (query.Page - 1) * query.PageSize;

        var spec = new Specifications.ActivitiesFilterSpec(
            query.ContactId, query.LeadId, skip, query.PageSize);

        var activities = await _activityRepository.ListAsync(spec, cancellationToken);

        var countSpec = new Specifications.ActivitiesCountSpec(query.ContactId, query.LeadId);
        var totalCount = await _activityRepository.CountAsync(countSpec, cancellationToken);

        var items = activities.Select(a => new Features.Crm.DTOs.ActivityDto(
            a.Id, a.Type, a.Subject, a.Description,
            a.ContactId, a.Contact?.FullName,
            a.LeadId, a.Lead?.Title,
            a.PerformedById,
            a.PerformedBy != null ? $"{a.PerformedBy.FirstName} {a.PerformedBy.LastName}" : "",
            a.PerformedAt, a.DurationMinutes,
            a.CreatedAt, a.ModifiedAt)).ToList();

        return Result.Success(PagedResult<Features.Crm.DTOs.ActivityDto>.Create(
            items, totalCount, query.Page - 1, query.PageSize));
    }
}
