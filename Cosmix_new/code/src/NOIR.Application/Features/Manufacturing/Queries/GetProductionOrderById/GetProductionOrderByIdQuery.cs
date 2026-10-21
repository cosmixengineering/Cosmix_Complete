namespace NOIR.Application.Features.Manufacturing.Queries.GetProductionOrderById;

/// <summary>
/// Query to get a production order by ID with its material requirement items.
/// </summary>
public sealed record GetProductionOrderByIdQuery(Guid Id);
