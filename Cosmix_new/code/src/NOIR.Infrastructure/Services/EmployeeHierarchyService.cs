namespace NOIR.Infrastructure.Services;

/// <summary>
/// Validates employee manager hierarchy using recursive CTE for circular reference and depth checks.
/// </summary>
public class EmployeeHierarchyService : IEmployeeHierarchyService, IScopedService
{
    // Direct DbContext injection for raw SQL recursive CTE query.
    private readonly ApplicationDbContext _dbContext;

    public EmployeeHierarchyService(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<HierarchyChain> GetAncestorChainAsync(
        Guid employeeId, int maxDepth, string? tenantId, CancellationToken ct)
    {
        // Recursive CTE walking up the manager chain
        var sql = tenantId != null
            ? @"
                WITH ManagerChain AS (
                    SELECT e.Id, e.ManagerId, 1 AS Depth
                    FROM Employees e
                    WHERE e.Id = {0} AND e.TenantId = {1} AND e.IsDeleted = 0
                    UNION ALL
                    SELECT e.Id, e.ManagerId, mc.Depth + 1
                    FROM Employees e
                    INNER JOIN ManagerChain mc ON e.Id = mc.ManagerId
                    WHERE e.IsDeleted = 0 AND mc.Depth < {2}
                )
                SELECT Id, Depth FROM ManagerChain
                OPTION (MAXRECURSION 20)"
            : @"
                WITH ManagerChain AS (
                    SELECT e.Id, e.ManagerId, 1 AS Depth
                    FROM Employees e
                    WHERE e.Id = {0} AND e.TenantId IS NULL AND e.IsDeleted = 0
                    UNION ALL
                    SELECT e.Id, e.ManagerId, mc.Depth + 1
                    FROM Employees e
                    INNER JOIN ManagerChain mc ON e.Id = mc.ManagerId
                    WHERE e.IsDeleted = 0 AND mc.Depth < {2}
                )
                SELECT Id, Depth FROM ManagerChain
                OPTION (MAXRECURSION 20)";

        var results = tenantId != null
            ? await _dbContext.Database.SqlQueryRaw<AncestorRow>(sql, employeeId, tenantId, maxDepth).ToListAsync(ct)
            : await _dbContext.Database.SqlQueryRaw<AncestorRow>(sql, employeeId, maxDepth).ToListAsync(ct);

        var ancestorIds = results.Select(r => r.Id).ToHashSet();
        var depth = results.Count > 0 ? results.Max(r => r.Depth) : 0;

        return new HierarchyChain(depth, ancestorIds);
    }
}

/// <summary>
/// Row shape for the recursive CTE result.
/// </summary>
internal record struct AncestorRow(Guid Id, int Depth);
