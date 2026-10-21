namespace NOIR.Application.Features.Manufacturing.Queries.GetBillOfMaterialById;

/// <summary>
/// Query to get a bill of material by ID with its component items.
/// </summary>
public sealed record GetBillOfMaterialByIdQuery(Guid Id);
