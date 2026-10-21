
namespace NOIR.Application.Features.Tenants.Queries.GetTenants;

/// <summary>
/// Wolverine handler for getting a paginated list of tenants.
/// Supports search and status filtering.
/// Uses Finbuckle's IMultiTenantStore for tenant retrieval.
/// </summary>
public class GetTenantsQueryHandler
{
    private readonly IMultiTenantStore<Tenant> _tenantStore;
    private readonly IUserDisplayNameService _userDisplayNameService;

    public GetTenantsQueryHandler(IMultiTenantStore<Tenant> tenantStore, IUserDisplayNameService userDisplayNameService)
    {
        _tenantStore = tenantStore;
        _userDisplayNameService = userDisplayNameService;
    }

    public async Task<Result<PaginatedList<TenantListDto>>> Handle(GetTenantsQuery query, CancellationToken cancellationToken)
    {
        // Get all tenants from store (Finbuckle handles query)
        var allTenants = await _tenantStore.GetAllAsync();

        // Apply filters in memory (since Finbuckle's GetAllAsync doesn't support filtering)
        var filteredTenants = allTenants
            .Where(t => !t.IsDeleted) // Exclude soft-deleted
            .AsQueryable();

        // Apply search filter
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.ToLowerInvariant();
            filteredTenants = filteredTenants.Where(t =>
                (t.Name != null && t.Name.ToLowerInvariant().Contains(search)) ||
                (t.Identifier != null && t.Identifier.ToLowerInvariant().Contains(search)));
        }

        // Apply status filter
        if (query.IsActive.HasValue)
        {
            filteredTenants = filteredTenants.Where(t => t.IsActive == query.IsActive.Value);
        }

        // Get total count before pagination
        var totalCount = filteredTenants.Count();

        // Sorting
        IOrderedQueryable<Tenant> sortedTenants = query.OrderBy?.ToLowerInvariant() switch
        {
            "name" => query.IsDescending ? filteredTenants.OrderByDescending(t => t.Name) : filteredTenants.OrderBy(t => t.Name),
            "identifier" => query.IsDescending ? filteredTenants.OrderByDescending(t => t.Identifier) : filteredTenants.OrderBy(t => t.Identifier),
            "status" or "isactive" => query.IsDescending ? filteredTenants.OrderByDescending(t => t.IsActive) : filteredTenants.OrderBy(t => t.IsActive),
            "createdat" => query.IsDescending ? filteredTenants.OrderByDescending(t => t.CreatedAt) : filteredTenants.OrderBy(t => t.CreatedAt),
            _ => filteredTenants.OrderBy(t => t.Name),
        };

        // Apply pagination
        var pagedTenants = sortedTenants
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToList();

        // Resolve user names
        var userIds = pagedTenants
            .SelectMany(x => new[] { x.CreatedBy, x.ModifiedBy })
            .Where(id => !string.IsNullOrEmpty(id))
            .Select(id => id!)
            .Distinct();
        var userNames = await _userDisplayNameService.GetDisplayNamesAsync(userIds, cancellationToken);

        // Map to DTOs
        var tenantDtos = pagedTenants.Select(t => new TenantListDto(
            t.Id,
            t.Identifier,
            t.Name,
            t.Domain,
            t.IsActive,
            t.CreatedAt,
            t.ModifiedAt,
            t.CreatedBy != null ? userNames.GetValueOrDefault(t.CreatedBy) : null,
            t.ModifiedBy != null ? userNames.GetValueOrDefault(t.ModifiedBy) : null
        )).ToList();

        var result = PaginatedList<TenantListDto>.Create(
            tenantDtos,
            totalCount,
            query.Page,
            query.PageSize);

        return Result.Success(result);
    }
}
