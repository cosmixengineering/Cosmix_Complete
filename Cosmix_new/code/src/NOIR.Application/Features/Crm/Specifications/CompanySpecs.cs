namespace NOIR.Application.Features.Crm.Specifications;

/// <summary>
/// Get company by ID with tracking for mutations.
/// </summary>
public sealed class CompanyByIdSpec : Specification<CrmCompany>
{
    public CompanyByIdSpec(Guid id)
    {
        Query.Where(c => c.Id == id)
             .Include(c => c.Owner!)
             .Include(c => c.Contacts)
             .AsTracking()
             .TagWith("CompanyById");
    }
}

/// <summary>
/// Get company by ID read-only (for queries).
/// </summary>
public sealed class CompanyByIdReadOnlySpec : Specification<CrmCompany>
{
    public CompanyByIdReadOnlySpec(Guid id)
    {
        Query.Where(c => c.Id == id)
             .Include(c => c.Owner!)
             .Include(c => c.Contacts)
             .TagWith("CompanyByIdReadOnly");
    }
}

/// <summary>
/// Find company by name for uniqueness check.
/// </summary>
public sealed class CompanyByNameSpec : Specification<CrmCompany>
{
    public CompanyByNameSpec(string name, Guid? excludeId = null)
    {
        Query.Where(c => c.Name == name.Trim())
             .Where(c => excludeId == null || c.Id != excludeId)
             .TagWith("CompanyByName");
    }
}

/// <summary>
/// Paginated, filterable company list.
/// </summary>
public sealed class CompaniesFilterSpec : Specification<CrmCompany>
{
    public CompaniesFilterSpec(
        string? search = null,
        int? skip = null,
        int? take = null,
        string? orderBy = null,
        bool isDescending = true)
    {
        if (!string.IsNullOrEmpty(search))
        {
            Query.Where(c =>
                c.Name.Contains(search) ||
                (c.Domain != null && c.Domain.Contains(search)));
        }

        Query.Include(c => c.Owner!)
             .Include(c => c.Contacts);

        // Sorting
        switch (orderBy?.ToLowerInvariant())
        {
            case "name":
                if (isDescending) Query.OrderByDescending(c => c.Name);
                else Query.OrderBy(c => c.Name);
                break;
            case "industry":
                if (isDescending) Query.OrderByDescending(c => c.Industry ?? string.Empty);
                else Query.OrderBy(c => c.Industry ?? string.Empty);
                break;
            case "domain":
                if (isDescending) Query.OrderByDescending(c => c.Domain ?? string.Empty);
                else Query.OrderBy(c => c.Domain ?? string.Empty);
                break;
            case "owner":
                if (isDescending) Query.OrderByDescending(c => c.Owner != null ? c.Owner.LastName : string.Empty);
                else Query.OrderBy(c => c.Owner != null ? c.Owner.LastName : string.Empty);
                break;
            case "contactcount":
                if (isDescending) Query.OrderByDescending(c => c.Contacts.Count);
                else Query.OrderBy(c => c.Contacts.Count);
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
            case "created":
            default:
                if (isDescending) Query.OrderByDescending(c => c.CreatedAt);
                else Query.OrderBy(c => c.CreatedAt);
                break;
        }

        if (skip.HasValue) Query.Skip(skip.Value);
        if (take.HasValue) Query.Take(take.Value);

        Query.TagWith("CompaniesFilter");
    }
}

/// <summary>
/// Count companies matching filters (without pagination).
/// </summary>
public sealed class CompaniesCountSpec : Specification<CrmCompany>
{
    public CompaniesCountSpec(string? search = null)
    {
        if (!string.IsNullOrEmpty(search))
        {
            Query.Where(c =>
                c.Name.Contains(search) ||
                (c.Domain != null && c.Domain.Contains(search)));
        }

        Query.TagWith("CompaniesCount");
    }
}

/// <summary>
/// Check if a company has contacts (for delete validation).
/// </summary>
public sealed class CompanyHasContactsSpec : Specification<CrmContact>
{
    public CompanyHasContactsSpec(Guid companyId)
    {
        Query.Where(c => c.CompanyId == companyId)
             .TagWith("CompanyHasContacts");
    }
}
