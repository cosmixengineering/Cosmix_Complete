namespace NOIR.Application.Features.Crm.Queries.GetActivityById;

public class GetActivityByIdQueryHandler
{
    private readonly IRepository<CrmActivity, Guid> _activityRepository;

    public GetActivityByIdQueryHandler(IRepository<CrmActivity, Guid> activityRepository)
    {
        _activityRepository = activityRepository;
    }

    public async Task<Result<Features.Crm.DTOs.ActivityDto>> Handle(
        GetActivityByIdQuery query,
        CancellationToken cancellationToken)
    {
        var spec = new Specifications.ActivityByIdReadOnlySpec(query.Id);
        var activity = await _activityRepository.FirstOrDefaultAsync(spec, cancellationToken);

        if (activity is null)
        {
            return Result.Failure<Features.Crm.DTOs.ActivityDto>(
                Error.NotFound($"Activity with ID '{query.Id}' not found.", "NOIR-CRM-040"));
        }

        return Result.Success(new Features.Crm.DTOs.ActivityDto(
            activity.Id, activity.Type, activity.Subject, activity.Description,
            activity.ContactId, activity.Contact?.FullName,
            activity.LeadId, activity.Lead?.Title,
            activity.PerformedById,
            activity.PerformedBy != null ? $"{activity.PerformedBy.FirstName} {activity.PerformedBy.LastName}" : "",
            activity.PerformedAt, activity.DurationMinutes,
            activity.CreatedAt, activity.ModifiedAt));
    }
}
