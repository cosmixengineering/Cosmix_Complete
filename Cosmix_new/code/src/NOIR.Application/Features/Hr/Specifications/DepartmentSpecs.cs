namespace NOIR.Application.Features.Hr.Specifications;

/// <summary>
/// Get department by ID with tracking for mutations.
/// </summary>
public sealed class DepartmentByIdSpec : Specification<Department>
{
    public DepartmentByIdSpec(Guid id)
    {
        Query.Where(d => d.Id == id)
             .AsTracking()
             .TagWith("DepartmentById");
    }
}

/// <summary>
/// Get department by ID read-only with includes.
/// </summary>
public sealed class DepartmentByIdReadOnlySpec : Specification<Department>
{
    public DepartmentByIdReadOnlySpec(Guid id)
    {
        Query.Where(d => d.Id == id)
             .Include(d => d.Manager!)
             .Include(d => d.ParentDepartment!)
             .Include(d => d.SubDepartments)
             .TagWith("DepartmentByIdReadOnly");
    }
}

/// <summary>
/// Check department code uniqueness per tenant.
/// </summary>
public sealed class DepartmentByCodeSpec : Specification<Department>
{
    public DepartmentByCodeSpec(string code, string? tenantId, Guid? excludeId = null)
    {
        Query.Where(d => d.Code == code.ToUpperInvariant())
             .Where(d => tenantId == null || d.TenantId == tenantId)
             .Where(d => excludeId == null || d.Id != excludeId)
             .TagWith("DepartmentByCode");
    }
}

/// <summary>
/// Get departments by parent (for tree building).
/// </summary>
public sealed class DepartmentsByParentSpec : Specification<Department>
{
    public DepartmentsByParentSpec(Guid? parentId)
    {
        if (parentId.HasValue)
        {
            Query.Where(d => d.ParentDepartmentId == parentId.Value);
        }
        else
        {
            Query.Where(d => d.ParentDepartmentId == null);
        }

        Query.OrderBy(d => d.SortOrder)
             .ThenBy(d => d.Name)
             .TagWith("DepartmentsByParent");
    }
}

/// <summary>
/// Get all departments for tree query.
/// </summary>
public sealed class AllDepartmentsSpec : Specification<Department>
{
    public AllDepartmentsSpec()
    {
        Query.Include(d => d.Manager!)
             .OrderBy(d => d.SortOrder)
             .ThenBy(d => d.Name)
             .TagWith("AllDepartments");
    }
}

/// <summary>
/// Check if a department has active sub-departments (for delete validation).
/// </summary>
public sealed class ActiveSubDepartmentsSpec : Specification<Department>
{
    public ActiveSubDepartmentsSpec(Guid parentId)
    {
        Query.Where(d => d.ParentDepartmentId == parentId && d.IsActive)
             .TagWith("ActiveSubDepartments");
    }
}

/// <summary>
/// Get departments where a specific employee is the manager (for deactivation cascade).
/// </summary>
public sealed class DepartmentsByManagerIdSpec : Specification<Department>
{
    public DepartmentsByManagerIdSpec(Guid managerId)
    {
        Query.Where(d => d.ManagerId == managerId)
             .AsTracking()
             .TagWith("DepartmentsByManagerId");
    }
}
