namespace NOIR.Application.Features.CustomerGroups.Specifications;

/// <summary>
/// Specification to find a customer group by ID.
/// </summary>
public sealed class CustomerGroupByIdSpec : Specification<CustomerGroup>
{
    public CustomerGroupByIdSpec(Guid id)
    {
        Query.Where(g => g.Id == id)
             .TagWith("GetCustomerGroupById");
    }
}

/// <summary>
/// Specification to find a customer group by ID for update (with tracking).
/// </summary>
public sealed class CustomerGroupByIdForUpdateSpec : Specification<CustomerGroup>
{
    public CustomerGroupByIdForUpdateSpec(Guid id)
    {
        Query.Where(g => g.Id == id)
             .AsTracking()
             .TagWith("GetCustomerGroupByIdForUpdate");
    }
}

/// <summary>
/// Specification to check if a customer group name exists (for uniqueness validation).
/// </summary>
public sealed class CustomerGroupNameExistsSpec : Specification<CustomerGroup>
{
    public CustomerGroupNameExistsSpec(string name, Guid? excludeId = null)
    {
        Query.Where(g => g.Name == name);

        if (excludeId.HasValue)
        {
            Query.Where(g => g.Id != excludeId.Value);
        }

        Query.TagWith("CheckCustomerGroupNameExists");
    }
}

/// <summary>
/// Specification for paginated customer group listing with filters.
/// </summary>
public sealed class CustomerGroupsPagedSpec : Specification<CustomerGroup>
{
    public CustomerGroupsPagedSpec(
        string? search,
        bool? isActive,
        int page,
        int pageSize,
        string? orderBy = null,
        bool isDescending = true)
    {
        if (!string.IsNullOrEmpty(search))
        {
            Query.Where(g => g.Name.Contains(search) || g.Slug.Contains(search));
        }

        if (isActive.HasValue)
        {
            Query.Where(g => g.IsActive == isActive.Value);
        }

        // Sorting
        switch (orderBy?.ToLowerInvariant())
        {
            case "name":
                if (isDescending) Query.OrderByDescending(g => g.Name);
                else Query.OrderBy(g => g.Name);
                break;
            case "slug":
                if (isDescending) Query.OrderByDescending(g => g.Slug);
                else Query.OrderBy(g => g.Slug);
                break;
            case "status":
            case "isactive":
                if (isDescending) Query.OrderByDescending(g => g.IsActive);
                else Query.OrderBy(g => g.IsActive);
                break;
            case "members":
            case "membercount":
                if (isDescending) Query.OrderByDescending(g => g.MemberCount);
                else Query.OrderBy(g => g.MemberCount);
                break;
            case "createdat":
                if (isDescending) Query.OrderByDescending(g => (object)g.CreatedAt);
                else Query.OrderBy(g => (object)g.CreatedAt);
                break;
            case "createdby":
            case "creator":
                if (isDescending) Query.OrderByDescending(g => g.CreatedBy);
                else Query.OrderBy(g => g.CreatedBy);
                break;
            case "modifiedby":
            case "editor":
                if (isDescending) Query.OrderByDescending(g => g.ModifiedBy);
                else Query.OrderBy(g => g.ModifiedBy);
                break;
            default:
                Query.OrderBy(g => g.Name);
                break;
        }

        Query.Skip((page - 1) * pageSize)
             .Take(pageSize)
             .TagWith("GetCustomerGroupsPaged");
    }
}

/// <summary>
/// Specification for counting customer groups with filters (for pagination).
/// </summary>
public sealed class CustomerGroupsCountSpec : Specification<CustomerGroup>
{
    public CustomerGroupsCountSpec(string? search, bool? isActive)
    {
        if (!string.IsNullOrEmpty(search))
        {
            Query.Where(g => g.Name.Contains(search) || g.Slug.Contains(search));
        }

        if (isActive.HasValue)
        {
            Query.Where(g => g.IsActive == isActive.Value);
        }

        Query.TagWith("CountCustomerGroups");
    }
}

/// <summary>
/// Specification to check if a customer group has any members.
/// </summary>
public sealed class CustomerGroupHasMembersSpec : Specification<CustomerGroupMembership>
{
    public CustomerGroupHasMembersSpec(Guid customerGroupId)
    {
        Query.Where(m => m.CustomerGroupId == customerGroupId)
             .TagWith("CheckCustomerGroupHasMembers");
    }
}

/// <summary>
/// Specification to get memberships by group ID and customer IDs.
/// </summary>
public sealed class MembershipsByGroupAndCustomersSpec : Specification<CustomerGroupMembership>
{
    public MembershipsByGroupAndCustomersSpec(Guid customerGroupId, List<Guid> customerIds)
    {
        Query.Where(m => m.CustomerGroupId == customerGroupId && customerIds.Contains(m.CustomerId))
             .AsTracking()
             .TagWith("GetMembershipsByGroupAndCustomers");
    }
}
