namespace NOIR.Application.Features.Products.Queries.ExportProductsFile;

/// <summary>
/// Wolverine handler for exporting products as a downloadable file (CSV or Excel).
/// Reuses ExportProductsQuery via IMessageBus for data fetching.
/// </summary>
public class ExportProductsFileQueryHandler
{
    private readonly IMessageBus _bus;
    private readonly IExcelExportService _excelExportService;
    private readonly ILogger<ExportProductsFileQueryHandler> _logger;

    public ExportProductsFileQueryHandler(
        IMessageBus bus,
        IExcelExportService excelExportService,
        ILogger<ExportProductsFileQueryHandler> logger)
    {
        _bus = bus;
        _excelExportService = excelExportService;
        _logger = logger;
    }

    public async Task<Result<ExportResultDto>> Handle(
        ExportProductsFileQuery query,
        CancellationToken cancellationToken)
    {
        // Reuse existing export handler to fetch product data
        var dataQuery = new ExportProducts.ExportProductsQuery(
            query.CategoryId,
            query.Status,
            query.IncludeAttributes,
            query.IncludeImages);

        var dataResult = await _bus.InvokeAsync<Result<ExportProducts.ExportProductsResultDto>>(dataQuery, cancellationToken);
        if (dataResult.IsFailure)
            return Result.Failure<ExportResultDto>(dataResult.Error);

        var data = dataResult.Value;
        var timestamp = DateTimeOffset.UtcNow.ToString("yyyyMMdd-HHmmss");

        // Build column headers
        var headers = new List<string>
        {
            "Name", "Slug", "SKU", "Barcode", "BasePrice", "Currency", "Status",
            "Category", "Brand", "ShortDescription", "VariantName", "VariantPrice",
            "CompareAtPrice", "Stock"
        };

        if (query.IncludeImages)
            headers.Add("Images");

        if (query.IncludeAttributes)
            headers.AddRange(data.AttributeColumns);

        // Build data rows
        var rows = new List<IReadOnlyList<object?>>();
        foreach (var row in data.Rows)
        {
            var rowData = new List<object?>
            {
                row.Name, row.Slug, row.Sku, row.Barcode, row.BasePrice, row.Currency,
                row.Status, row.CategoryName, row.Brand, row.ShortDescription,
                row.VariantName, row.VariantPrice, row.CompareAtPrice, row.Stock
            };

            if (query.IncludeImages)
                rowData.Add(row.Images);

            if (query.IncludeAttributes)
            {
                foreach (var attrCol in data.AttributeColumns)
                {
                    row.Attributes.TryGetValue(attrCol, out var attrValue);
                    rowData.Add(attrValue);
                }
            }

            rows.Add(rowData);
        }

        byte[] fileBytes;
        string contentType;
        string fileName;

        if (query.Format == ExportFormat.Excel)
        {
            fileBytes = _excelExportService.CreateExcelFile("Products", headers, rows);
            contentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
            fileName = $"products-{timestamp}.xlsx";
        }
        else
        {
            var sb = new StringBuilder();
            sb.AppendLine(string.Join(",", headers.Select(h => $"\"{h}\"")));

            foreach (var row in rows)
            {
                sb.AppendLine(string.Join(",", row.Select(v => v is null ? "" : $"\"{EscapeCsv(v.ToString())}\"")));
            }

            fileBytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(sb.ToString())).ToArray();
            contentType = "text/csv";
            fileName = $"products-{timestamp}.csv";
        }

        _logger.LogInformation("Exported {RowCount} product rows as {Format}", data.Rows.Count, query.Format);

        return Result.Success(new ExportResultDto(fileBytes, contentType, fileName));
    }

    private static string EscapeCsv(string? value)
    {
        if (string.IsNullOrEmpty(value)) return string.Empty;
        return value.Replace("\"", "\"\"");
    }
}
