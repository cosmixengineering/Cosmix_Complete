namespace NOIR.Application.Features.Manufacturing.Queries.GetProductionOrders;

/// <summary>
/// Query to get paged list of production orders.
/// </summary>
public sealed record GetProductionOrdersQuery(
    string? Search = null,
    ProductionOrderStatus? Status = null,
    int Page = 1,
    int PageSize = 20);
