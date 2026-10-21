namespace NOIR.Application.Features.Products.Queries.ExportProductsFile;

/// <summary>
/// Query to export products as a downloadable file (CSV or Excel).
/// </summary>
public sealed record ExportProductsFileQuery(
    ExportFormat Format = ExportFormat.CSV,
    string? CategoryId = null,
    string? Status = null,
    bool IncludeAttributes = true,
    bool IncludeImages = true);
