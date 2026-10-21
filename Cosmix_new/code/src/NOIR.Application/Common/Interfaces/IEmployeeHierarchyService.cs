namespace NOIR.Application.Common.Interfaces;

/// <summary>
/// Service for validating employee manager hierarchy (circular reference and depth checks).
/// </summary>
public interface IEmployeeHierarchyService
{
    /// <summary>
    /// Returns ancestor chain of the given employee up to maxDepth.
    /// Used for circular reference and depth validation.
    /// </summary>
    Task<HierarchyChain> GetAncestorChainAsync(
        Guid employeeId, int maxDepth, string? tenantId, CancellationToken ct);
}

/// <summary>
/// Result of walking the ancestor chain of an employee.
/// </summary>
public record HierarchyChain(int Depth, HashSet<Guid> AncestorIds);
