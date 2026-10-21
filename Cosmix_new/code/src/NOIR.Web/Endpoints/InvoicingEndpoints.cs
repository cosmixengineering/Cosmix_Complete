namespace NOIR.Web.Endpoints;

public static class InvoicingEndpoints
{
    public static void MapInvoicingEndpoints(this IEndpointRouteBuilder app)
    {
        var invoiceGroup = app.MapGroup("/api/invoicing/invoices")
            .WithTags("Invoicing - Sales Invoices")
            .RequireAuthorization();

        // Get invoices
        invoiceGroup.MapGet("/", async ([FromQuery] InvoiceStatus? status, [FromQuery] Guid? customerId, IApplicationDbContext dbContext) =>
        {
            var query = dbContext.SalesInvoices.Include(x => x.Items).AsNoTracking();
            if (status.HasValue)
            {
                query = query.Where(x => x.Status == status.Value);
            }
            if (customerId.HasValue)
            {
                query = query.Where(x => x.CustomerId == customerId.Value);
            }
            var list = await query.OrderByDescending(x => x.IssueDate).ToListAsync();
            return Results.Ok(list);
        })
        .WithName("GetSalesInvoices");

        // Create invoice
        invoiceGroup.MapPost("/", async (CreateInvoiceRequest req, IApplicationDbContext dbContext, IUnitOfWork unitOfWork) =>
        {
            var invoice = SalesInvoice.Create(
                req.InvoiceNumber,
                req.CustomerId,
                req.ProjectId,
                req.DueDate,
                req.Currency,
                req.PaymentTerms,
                req.Notes);

            if (req.Items != null)
            {
                foreach (var item in req.Items)
                {
                    invoice.AddItem(item.ProductId, item.Description, item.Quantity, item.Unit, item.UnitPrice, item.TaxRate);
                }
            }

            dbContext.SalesInvoices.Add(invoice);
            await unitOfWork.SaveChangesAsync();
            return Results.Created($"/api/invoicing/invoices/{invoice.Id}", invoice);
        })
        .WithName("CreateSalesInvoice");

        // Issue invoice
        invoiceGroup.MapPost("/{id:guid}/issue", async (Guid id, IApplicationDbContext dbContext, IUnitOfWork unitOfWork) =>
        {
            var invoice = await dbContext.SalesInvoices.FindAsync(id);
            if (invoice is null) return Results.NotFound();

            invoice.Issue();
            await unitOfWork.SaveChangesAsync();
            return Results.Ok(invoice);
        })
        .WithName("IssueSalesInvoice");

        var paymentGroup = app.MapGroup("/api/invoicing/payments")
            .WithTags("Invoicing - Payments")
            .RequireAuthorization();

        // Record payment against invoice
        paymentGroup.MapPost("/", async (CreatePaymentRecordRequest req, IApplicationDbContext dbContext, IUnitOfWork unitOfWork) =>
        {
            var invoice = await dbContext.SalesInvoices.FindAsync(req.SalesInvoiceId);
            if (invoice is null) return Results.NotFound("Sales invoice not found.");

            var payment = PaymentRecord.Create(
                req.PaymentNumber,
                req.CustomerId,
                req.SalesInvoiceId,
                req.Amount,
                req.Method,
                req.TransactionReference,
                req.Notes);

            invoice.RecordPayment(req.Amount);

            dbContext.PaymentRecords.Add(payment);
            await unitOfWork.SaveChangesAsync();
            return Results.Created($"/api/invoicing/payments/{payment.Id}", payment);
        })
        .WithName("RecordPayment");
    }
}

public record CreateInvoiceRequest(
    string InvoiceNumber,
    Guid CustomerId,
    Guid? ProjectId,
    DateTimeOffset DueDate,
    string Currency,
    string? PaymentTerms,
    string? Notes,
    List<CreateInvoiceItemRequest>? Items);

public record CreateInvoiceItemRequest(
    Guid ProductId,
    string Description,
    decimal Quantity,
    string Unit,
    decimal UnitPrice,
    decimal TaxRate);

public record CreatePaymentRecordRequest(
    string PaymentNumber,
    Guid CustomerId,
    Guid SalesInvoiceId,
    decimal Amount,
    InvoicePaymentMethod Method,
    string? TransactionReference,
    string? Notes);
