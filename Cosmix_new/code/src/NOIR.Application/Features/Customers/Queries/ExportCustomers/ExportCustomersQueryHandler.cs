namespace NOIR.Application.Features.Customers.Queries.ExportCustomers;

/// <summary>
/// Wolverine handler for exporting customers as a downloadable file.
/// </summary>
public class ExportCustomersQueryHandler
{
    private readonly IRepository<Domain.Entities.Customer.Customer, Guid> _customerRepository;
    private readonly IExcelExportService _excelExportService;
    private readonly ILogger<ExportCustomersQueryHandler> _logger;

    public ExportCustomersQueryHandler(
        IRepository<Domain.Entities.Customer.Customer, Guid> customerRepository,
        IExcelExportService excelExportService,
        ILogger<ExportCustomersQueryHandler> logger)
    {
        _customerRepository = customerRepository;
        _excelExportService = excelExportService;
        _logger = logger;
    }

    public async Task<Result<ExportResultDto>> Handle(
        ExportCustomersQuery query,
        CancellationToken cancellationToken)
    {
        var spec = new CustomersForExportSpec(query.Search, query.Segment, query.Tier, query.IsActive);
        var customers = await _customerRepository.ListAsync(spec, cancellationToken);

        var headers = new List<string>
        {
            "FirstName", "LastName", "Email", "Phone", "Segment", "Tier",
            "TotalOrders", "TotalSpent", "AverageOrderValue", "LoyaltyPoints",
            "IsActive", "Tags", "CreatedAt"
        };

        var rows = new List<IReadOnlyList<object?>>();
        foreach (var c in customers)
        {
            rows.Add(new List<object?>
            {
                c.FirstName, c.LastName, c.Email, c.Phone,
                c.Segment.ToString(), c.Tier.ToString(),
                c.TotalOrders, c.TotalSpent, c.AverageOrderValue, c.LoyaltyPoints,
                c.IsActive, c.Tags, c.CreatedAt
            });
        }

        var timestamp = DateTimeOffset.UtcNow.ToString("yyyyMMdd-HHmmss");
        byte[] fileBytes;
        string contentType;
        string fileName;

        if (query.Format == ExportFormat.Excel)
        {
            fileBytes = _excelExportService.CreateExcelFile("Customers", headers, rows);
            contentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
            fileName = $"customers-{timestamp}.xlsx";
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
            fileName = $"customers-{timestamp}.csv";
        }

        _logger.LogInformation("Exported {CustomerCount} customers as {Format}", customers.Count, query.Format);

        return Result.Success(new ExportResultDto(fileBytes, contentType, fileName));
    }

    private static string EscapeCsv(string? value)
    {
        if (string.IsNullOrEmpty(value)) return string.Empty;
        return value.Replace("\"", "\"\"");
    }
}
