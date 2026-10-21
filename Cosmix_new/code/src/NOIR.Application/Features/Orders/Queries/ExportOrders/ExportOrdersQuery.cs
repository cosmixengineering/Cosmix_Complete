namespace NOIR.Application.Features.Orders.Queries.ExportOrders;

/// <summary>
/// Query to export orders as a downloadable file (CSV or Excel).
/// </summary>
public sealed record ExportOrdersQuery(
    ExportFormat Format = ExportFormat.CSV,
    OrderStatus? Status = null,
    string? CustomerEmail = null,
    DateTimeOffset? FromDate = null,
    DateTimeOffset? ToDate = null);
