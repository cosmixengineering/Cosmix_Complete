namespace NOIR.Application.Features.Customers.Queries.ExportCustomers;

/// <summary>
/// Query to export customers as a downloadable file (CSV or Excel).
/// </summary>
public sealed record ExportCustomersQuery(
    ExportFormat Format = ExportFormat.CSV,
    CustomerSegment? Segment = null,
    CustomerTier? Tier = null,
    bool? IsActive = null,
    string? Search = null);
