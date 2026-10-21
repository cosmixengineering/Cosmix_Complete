using NOIR.Domain.Entities.Commercial;
using NOIR.Domain.Entities.Estimation;

namespace NOIR.Web.Endpoints;

public static class CustomerPoEndpoints
{
    public static void MapCustomerPoEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/commercial/customer-pos")
            .WithTags("Commercial - Customer Purchase Orders")
            .RequireAuthorization();

        // Get Customer POs
        group.MapGet("/", async (IApplicationDbContext dbContext) =>
        {
            var pos = await dbContext.CustomerPurchaseOrders.AsNoTracking().OrderByDescending(x => x.PoDate).ToListAsync();
            return Results.Ok(pos);
        })
        .WithName("GetCustomerPurchaseOrders");

        // Create Customer PO
        group.MapPost("/", async (CreateCustomerPoRequest req, IApplicationDbContext dbContext, IUnitOfWork unitOfWork) =>
        {
            var po = CustomerPurchaseOrder.Create(
                req.CustomerPoNumber,
                req.CustomerId,
                req.ProjectId,
                req.BoqId,
                req.PoDate,
                req.PoAmount,
                req.Currency,
                req.DocumentUrl,
                req.DeliveryTerms,
                req.PaymentTerms,
                req.ScopeOfWork);

            dbContext.CustomerPurchaseOrders.Add(po);
            await unitOfWork.SaveChangesAsync();
            return Results.Created($"/api/commercial/customer-pos/{po.Id}", po);
        })
        .WithName("CreateCustomerPurchaseOrder");

        // Reconcile Customer PO against Quotation (Ported from MatchFlow MatchingService)
        group.MapPost("/{id:guid}/reconcile-with-quote/{boqId:guid}", async (Guid id, Guid boqId, IApplicationDbContext dbContext, IUnitOfWork unitOfWork) =>
        {
            var po = await dbContext.CustomerPurchaseOrders.FindAsync(id);
            if (po is null) return Results.NotFound("Customer PO not found.");

            var boq = await dbContext.Boqs.Include(x => x.Items).FirstOrDefaultAsync(x => x.Id == boqId);
            if (boq is null) return Results.NotFound("Quotation BOQ not found.");

            var poItems = new List<(string Code, decimal Qty, decimal Rate)>(); // extensible from line items
            var quoteItems = boq.Items.Select(x => (x.ItemCode, x.Quantity, x.UnitRate)).ToList();

            var matchResult = PoReconciliationEngine.Reconcile(
                po.PoAmount,
                boq.TotalAmount,
                po.Currency,
                boq.Currency,
                poItems,
                quoteItems);

            if (matchResult.IsMatched)
            {
                po.MarkReconciled();
            }
            else
            {
                po.RecordDiscrepancy(matchResult.Reason ?? "Discrepancy identified between PO and Quotation");
                var exceptionRecord = new ExceptionRecord(po.Id, "CustomerPO", matchResult.Reason ?? "Mismatch");
                dbContext.ExceptionRecords.Add(exceptionRecord);
            }

            await unitOfWork.SaveChangesAsync();
            return Results.Ok(matchResult);
        })
        .WithName("ReconcileCustomerPoWithQuote");
    }
}

public record CreateCustomerPoRequest(
    string CustomerPoNumber,
    Guid CustomerId,
    Guid? ProjectId,
    Guid? BoqId,
    DateTimeOffset PoDate,
    decimal PoAmount,
    string Currency,
    string? DocumentUrl,
    string? DeliveryTerms,
    string? PaymentTerms,
    string? ScopeOfWork);
