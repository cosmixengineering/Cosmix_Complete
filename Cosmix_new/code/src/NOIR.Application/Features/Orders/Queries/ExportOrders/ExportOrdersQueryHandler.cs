namespace NOIR.Application.Features.Orders.Queries.ExportOrders;

/// <summary>
/// Wolverine handler for exporting orders as a downloadable file.
/// </summary>
public class ExportOrdersQueryHandler
{
    private readonly IRepository<Order, Guid> _orderRepository;
    private readonly IExcelExportService _excelExportService;
    private readonly ILogger<ExportOrdersQueryHandler> _logger;

    public ExportOrdersQueryHandler(
        IRepository<Order, Guid> orderRepository,
        IExcelExportService excelExportService,
        ILogger<ExportOrdersQueryHandler> logger)
    {
        _orderRepository = orderRepository;
        _excelExportService = excelExportService;
        _logger = logger;
    }

    public async Task<Result<ExportResultDto>> Handle(
        ExportOrdersQuery query,
        CancellationToken cancellationToken)
    {
        var spec = new OrdersForExportSpec(query.Status, query.CustomerEmail, query.FromDate, query.ToDate);
        var orders = await _orderRepository.ListAsync(spec, cancellationToken);

        var headers = new List<string>
        {
            "OrderNumber", "Status", "CustomerEmail", "CustomerName",
            "SubTotal", "DiscountAmount", "ShippingAmount", "TaxAmount", "GrandTotal",
            "Currency", "CouponCode", "ShippingMethod", "TrackingNumber",
            "CreatedAt", "ConfirmedAt", "ShippedAt", "DeliveredAt", "CompletedAt"
        };

        var rows = new List<IReadOnlyList<object?>>();
        foreach (var o in orders)
        {
            rows.Add(new List<object?>
            {
                o.OrderNumber, o.Status.ToString(), o.CustomerEmail, o.CustomerName,
                o.SubTotal, o.DiscountAmount, o.ShippingAmount, o.TaxAmount, o.GrandTotal,
                o.Currency, o.CouponCode, o.ShippingMethod, o.TrackingNumber,
                o.CreatedAt, o.ConfirmedAt, o.ShippedAt, o.DeliveredAt, o.CompletedAt
            });
        }

        var timestamp = DateTimeOffset.UtcNow.ToString("yyyyMMdd-HHmmss");
        byte[] fileBytes;
        string contentType;
        string fileName;

        if (query.Format == ExportFormat.Excel)
        {
            fileBytes = _excelExportService.CreateExcelFile("Orders", headers, rows);
            contentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
            fileName = $"orders-{timestamp}.xlsx";
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
            fileName = $"orders-{timestamp}.csv";
        }

        _logger.LogInformation("Exported {OrderCount} orders as {Format}", orders.Count, query.Format);

        return Result.Success(new ExportResultDto(fileBytes, contentType, fileName));
    }

    private static string EscapeCsv(string? value)
    {
        if (string.IsNullOrEmpty(value)) return string.Empty;
        return value.Replace("\"", "\"\"");
    }
}
