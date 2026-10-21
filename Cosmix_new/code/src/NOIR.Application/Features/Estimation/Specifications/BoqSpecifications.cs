namespace NOIR.Application.Features.Estimation.Specifications;

/// <summary>
/// Specification for paginated BOQ listing with filters.
/// </summary>
public sealed class BoqsByFilterSpec : Specification<Boq>
{
    public BoqsByFilterSpec(string? search, BoqStatus? status, int page, int pageSize)
    {
        if (!string.IsNullOrEmpty(search))
        {
            Query.Where(b => b.BoqNumber.Contains(search) || b.Title.Contains(search));
        }

        if (status.HasValue)
        {
            Query.Where(b => b.Status == status.Value);
        }

        Query.Include(b => b.Items);

        Query.OrderByDescending(b => b.CreatedAt)
             .Skip((page - 1) * pageSize)
             .Take(pageSize)
             .TagWith("GetBoqs");
    }
}

/// <summary>
/// Specification for counting BOQs with filters (for pagination).
/// </summary>
public sealed class BoqsCountSpec : Specification<Boq>
{
    public BoqsCountSpec(string? search, BoqStatus? status)
    {
        if (!string.IsNullOrEmpty(search))
        {
            Query.Where(b => b.BoqNumber.Contains(search) || b.Title.Contains(search));
        }

        if (status.HasValue)
        {
            Query.Where(b => b.Status == status.Value);
        }

        Query.TagWith("CountBoqs");
    }
}

/// <summary>
/// Specification to find a BOQ by ID including items and revisions.
/// </summary>
public sealed class BoqByIdSpec : Specification<Boq>
{
    public BoqByIdSpec(Guid id)
    {
        Query.Where(b => b.Id == id)
             .Include(b => b.Items)
             .Include(b => b.Revisions)
             .TagWith("GetBoqById");
    }
}

/// <summary>
/// Specification to find a BOQ by ID for update (with tracking), including items and revisions.
/// </summary>
public sealed class BoqByIdForUpdateSpec : Specification<Boq>
{
    public BoqByIdForUpdateSpec(Guid id)
    {
        Query.Where(b => b.Id == id)
             .Include(b => b.Items)
             .Include(b => b.Revisions)
             .AsTracking()
             .TagWith("GetBoqByIdForUpdate");
    }
}

/// <summary>
/// Specification to check if a BOQ number exists (for uniqueness validation).
/// </summary>
public sealed class BoqNumberExistsSpec : Specification<Boq>
{
    public BoqNumberExistsSpec(string boqNumber, Guid? excludeId = null)
    {
        Query.Where(b => b.BoqNumber == boqNumber);

        if (excludeId.HasValue)
        {
            Query.Where(b => b.Id != excludeId.Value);
        }

        Query.TagWith("CheckBoqNumberExists");
    }
}
