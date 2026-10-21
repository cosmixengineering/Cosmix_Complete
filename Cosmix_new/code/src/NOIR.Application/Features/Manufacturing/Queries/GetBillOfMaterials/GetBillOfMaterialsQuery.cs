namespace NOIR.Application.Features.Manufacturing.Queries.GetBillOfMaterials;

/// <summary>
/// Query to get paged list of bills of material.
/// </summary>
public sealed record GetBillOfMaterialsQuery(
    string? Search = null,
    bool? IsActive = null,
    int Page = 1,
    int PageSize = 20);
