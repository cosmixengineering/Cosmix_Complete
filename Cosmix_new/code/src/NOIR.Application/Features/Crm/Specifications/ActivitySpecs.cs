namespace NOIR.Application.Features.Crm.Specifications;

/// <summary>
/// Get activity by ID with tracking for mutations.
/// </summary>
public sealed class ActivityByIdSpec : Specification<CrmActivity>
{
    public ActivityByIdSpec(Guid id)
    {
        Query.Where(a => a.Id == id)
             .Include(a => a.Contact!)
             .Include(a => a.Lead!)
             .Include(a => a.PerformedBy!)
             .AsTracking()
             .TagWith("ActivityById");
    }
}

/// <summary>
/// Get activity by ID read-only (for queries).
/// </summary>
public sealed class ActivityByIdReadOnlySpec : Specification<CrmActivity>
{
    public ActivityByIdReadOnlySpec(Guid id)
    {
        Query.Where(a => a.Id == id)
             .Include(a => a.Contact!)
             .Include(a => a.Lead!)
             .Include(a => a.PerformedBy!)
             .TagWith("ActivityByIdReadOnly");
    }
}

/// <summary>
/// Paginated, filterable activity list.
/// </summary>
public sealed class ActivitiesFilterSpec : Specification<CrmActivity>
{
    public ActivitiesFilterSpec(
        Guid? contactId = null,
        Guid? leadId = null,
        int? skip = null,
        int? take = null)
    {
        if (contactId.HasValue)
            Query.Where(a => a.ContactId == contactId.Value);

        if (leadId.HasValue)
            Query.Where(a => a.LeadId == leadId.Value);

        Query.Include(a => a.Contact!)
             .Include(a => a.Lead!)
             .Include(a => a.PerformedBy!)
             .OrderByDescending(a => a.PerformedAt);

        if (skip.HasValue) Query.Skip(skip.Value);
        if (take.HasValue) Query.Take(take.Value);

        Query.TagWith("ActivitiesFilter");
    }
}

/// <summary>
/// Count activities matching filters (without pagination).
/// </summary>
public sealed class ActivitiesCountSpec : Specification<CrmActivity>
{
    public ActivitiesCountSpec(
        Guid? contactId = null,
        Guid? leadId = null)
    {
        if (contactId.HasValue)
            Query.Where(a => a.ContactId == contactId.Value);

        if (leadId.HasValue)
            Query.Where(a => a.LeadId == leadId.Value);

        Query.TagWith("ActivitiesCount");
    }
}
