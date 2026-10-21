namespace NOIR.Application.Features.Crm.Specifications;

/// <summary>
/// Get contact by ID with tracking for mutations.
/// </summary>
public sealed class ContactByIdSpec : Specification<CrmContact>
{
    public ContactByIdSpec(Guid id)
    {
        Query.Where(c => c.Id == id)
             .Include(c => c.Company!)
             .Include(c => c.Owner!)
             .Include(c => c.Customer!)
             .Include(c => c.Leads)
             .AsTracking()
             .TagWith("ContactById");
    }
}

/// <summary>
/// Get contact by ID read-only (for queries).
/// </summary>
public sealed class ContactByIdReadOnlySpec : Specification<CrmContact>
{
    public ContactByIdReadOnlySpec(Guid id)
    {
        Query.Where(c => c.Id == id)
             .Include(c => c.Company!)
             .Include(c => c.Owner!)
             .Include(c => c.Customer!)
             .Include(c => c.Leads)
             .TagWith("ContactByIdReadOnly");
    }
}

/// <summary>
/// Find contact by email for uniqueness check.
/// </summary>
public sealed class ContactByEmailSpec : Specification<CrmContact>
{
    public ContactByEmailSpec(string email, Guid? excludeId = null)
    {
        Query.Where(c => c.Email == email.ToLowerInvariant())
             .Where(c => excludeId == null || c.Id != excludeId)
             .TagWith("ContactByEmail");
    }
}

/// <summary>
/// Paginated, filterable contact list.
/// </summary>
public sealed class ContactsFilterSpec : Specification<CrmContact>
{
    public ContactsFilterSpec(
        string? search = null,
        Guid? companyId = null,
        Guid? ownerId = null,
        ContactSource? source = null,
        int? skip = null,
        int? take = null,
        string? orderBy = null,
        bool isDescending = true)
    {
        if (!string.IsNullOrEmpty(search))
        {
            Query.Where(c =>
                c.FirstName.Contains(search) ||
                c.LastName.Contains(search) ||
                c.Email.Contains(search));
        }

        if (companyId.HasValue)
            Query.Where(c => c.CompanyId == companyId.Value);

        if (ownerId.HasValue)
            Query.Where(c => c.OwnerId == ownerId.Value);

        if (source.HasValue)
            Query.Where(c => c.Source == source.Value);

        Query.Include(c => c.Company!)
             .Include(c => c.Owner!);

        // Sorting
        switch (orderBy?.ToLowerInvariant())
        {
            case "name":
                if (isDescending)
                    Query.OrderByDescending(c => c.LastName).ThenByDescending(c => c.FirstName);
                else
                    Query.OrderBy(c => c.LastName).ThenBy(c => c.FirstName);
                break;
            case "email":
                if (isDescending) Query.OrderByDescending(c => c.Email);
                else Query.OrderBy(c => c.Email);
                break;
            case "company":
                if (isDescending) Query.OrderByDescending(c => c.Company != null ? c.Company.Name : string.Empty);
                else Query.OrderBy(c => c.Company != null ? c.Company.Name : string.Empty);
                break;
            case "source":
                if (isDescending) Query.OrderByDescending(c => c.Source);
                else Query.OrderBy(c => c.Source);
                break;
            case "phone":
                if (isDescending) Query.OrderByDescending(c => c.Phone ?? string.Empty);
                else Query.OrderBy(c => c.Phone ?? string.Empty);
                break;
            case "createdby":
            case "creator":
                if (isDescending) Query.OrderByDescending(c => c.CreatedBy);
                else Query.OrderBy(c => c.CreatedBy);
                break;
            case "modifiedby":
            case "editor":
                if (isDescending) Query.OrderByDescending(c => c.ModifiedBy);
                else Query.OrderBy(c => c.ModifiedBy);
                break;
            case "createdat":
            default:
                if (isDescending) Query.OrderByDescending(c => c.CreatedAt);
                else Query.OrderBy(c => c.CreatedAt);
                break;
        }

        if (skip.HasValue) Query.Skip(skip.Value);
        if (take.HasValue) Query.Take(take.Value);

        Query.TagWith("ContactsFilter");
    }
}

/// <summary>
/// Count contacts matching filters (without pagination).
/// </summary>
public sealed class ContactsCountSpec : Specification<CrmContact>
{
    public ContactsCountSpec(
        string? search = null,
        Guid? companyId = null,
        Guid? ownerId = null,
        ContactSource? source = null)
    {
        if (!string.IsNullOrEmpty(search))
        {
            Query.Where(c =>
                c.FirstName.Contains(search) ||
                c.LastName.Contains(search) ||
                c.Email.Contains(search));
        }

        if (companyId.HasValue)
            Query.Where(c => c.CompanyId == companyId.Value);

        if (ownerId.HasValue)
            Query.Where(c => c.OwnerId == ownerId.Value);

        if (source.HasValue)
            Query.Where(c => c.Source == source.Value);

        Query.TagWith("ContactsCount");
    }
}

/// <summary>
/// Check if a contact has active leads (for delete validation).
/// </summary>
public sealed class ContactHasActiveLeadsSpec : Specification<Lead>
{
    public ContactHasActiveLeadsSpec(Guid contactId)
    {
        Query.Where(l => l.ContactId == contactId && l.Status == LeadStatus.Active)
             .TagWith("ContactHasActiveLeads");
    }
}
