using NOIR.Domain.Entities.Procurement;

namespace NOIR.Web.Endpoints;

public static class ProcurementEndpoints
{
    public static void MapProcurementEndpoints(this IEndpointRouteBuilder app)
    {
        var supplierGroup = app.MapGroup("/api/procurement/suppliers")
            .WithTags("Procurement - Suppliers")
            .RequireAuthorization();

        // Get suppliers
        supplierGroup.MapGet("/", async (IApplicationDbContext dbContext) =>
        {
            var suppliers = await dbContext.Suppliers.AsNoTracking().OrderBy(x => x.Name).ToListAsync();
            return Results.Ok(suppliers);
        })
        .WithName("GetSuppliers");

        // Create supplier
        supplierGroup.MapPost("/", async (CreateSupplierRequest req, IApplicationDbContext dbContext, IUnitOfWork unitOfWork) =>
        {
            var supplier = Supplier.Create(
                req.Code,
                req.Name,
                req.ContactPerson,
                req.Email,
                req.Phone,
                req.TaxId,
                req.Address,
                req.PaymentTerms,
                req.Notes);

            dbContext.Suppliers.Add(supplier);
            await unitOfWork.SaveChangesAsync();
            return Results.Created($"/api/procurement/suppliers/{supplier.Id}", supplier);
        })
        .WithName("CreateSupplier");

        var poGroup = app.MapGroup("/api/procurement/orders")
            .WithTags("Procurement - Purchase Orders")
            .RequireAuthorization();

        // Get POs
        poGroup.MapGet("/", async ([FromQuery] PurchaseOrderStatus? status, IApplicationDbContext dbContext) =>
        {
            var query = dbContext.PurchaseOrders.Include(x => x.Items).AsNoTracking();
            if (status.HasValue)
            {
                query = query.Where(x => x.Status == status.Value);
            }
            var orders = await query.OrderByDescending(x => x.OrderDate).ToListAsync();
            return Results.Ok(orders);
        })
        .WithName("GetPurchaseOrders");

        // Get PO by ID
        poGroup.MapGet("/{id:guid}", async (Guid id, IApplicationDbContext dbContext) =>
        {
            var po = await dbContext.PurchaseOrders.Include(x => x.Items).FirstOrDefaultAsync(x => x.Id == id);
            return po is not null ? Results.Ok(po) : Results.NotFound();
        })
        .WithName("GetPurchaseOrderById");

        // Create PO
        poGroup.MapPost("/", async (CreatePoRequest req, IApplicationDbContext dbContext, IUnitOfWork unitOfWork) =>
        {
            var po = PurchaseOrder.Create(
                req.PoNumber,
                req.SupplierId,
                req.ProjectId,
                req.ExpectedDeliveryDate,
                req.Currency,
                req.PaymentTerms,
                req.ShippingTerms,
                req.Notes);

            if (req.Items != null)
            {
                foreach (var item in req.Items)
                {
                    po.AddItem(item.ProductId, item.ItemDescription, item.Quantity, item.Unit, item.UnitPrice);
                }
            }

            dbContext.PurchaseOrders.Add(po);
            await unitOfWork.SaveChangesAsync();
            return Results.Created($"/api/procurement/orders/{po.Id}", po);
        })
        .WithName("CreatePurchaseOrder");

        // Approve PO
        poGroup.MapPost("/{id:guid}/approve", async (Guid id, IApplicationDbContext dbContext, IUnitOfWork unitOfWork) =>
        {
            var po = await dbContext.PurchaseOrders.FindAsync(id);
            if (po is null) return Results.NotFound();
            po.Approve();
            await unitOfWork.SaveChangesAsync();
            return Results.Ok(po);
        })
        .WithName("ApprovePurchaseOrder");
    }
}

public record CreateSupplierRequest(
    string Code,
    string Name,
    string? ContactPerson,
    string? Email,
    string? Phone,
    string? TaxId,
    string? Address,
    string? PaymentTerms,
    string? Notes);

public record CreatePoRequest(
    string PoNumber,
    Guid SupplierId,
    Guid? ProjectId,
    DateTimeOffset? ExpectedDeliveryDate,
    string Currency,
    string? PaymentTerms,
    string? ShippingTerms,
    string? Notes,
    List<CreatePoItemRequest>? Items);

public record CreatePoItemRequest(
    Guid ProductId,
    string ItemDescription,
    decimal Quantity,
    string Unit,
    decimal UnitPrice);
