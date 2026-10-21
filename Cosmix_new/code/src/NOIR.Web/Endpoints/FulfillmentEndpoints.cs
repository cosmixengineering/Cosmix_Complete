using NOIR.Domain.Entities.Fulfillment;
using NOIR.Domain.Entities.Stores;

namespace NOIR.Web.Endpoints;

public static class FulfillmentEndpoints
{
    public static void MapFulfillmentEndpoints(this IEndpointRouteBuilder app)
    {
        var challanGroup = app.MapGroup("/api/fulfillment/challans")
            .WithTags("Fulfillment - Delivery Challans")
            .RequireAuthorization();

        // Get challans
        challanGroup.MapGet("/", async ([FromQuery] DeliveryChallanStatus? status, IApplicationDbContext dbContext) =>
        {
            var query = dbContext.DeliveryChallans.Include(x => x.Items).AsNoTracking();
            if (status.HasValue)
            {
                query = query.Where(x => x.Status == status.Value);
            }
            var list = await query.OrderByDescending(x => x.DispatchDate).ToListAsync();
            return Results.Ok(list);
        })
        .WithName("GetDeliveryChallans");

        // Create challan
        challanGroup.MapPost("/", async (CreateChallanRequest req, IApplicationDbContext dbContext, IUnitOfWork unitOfWork) =>
        {
            var challan = DeliveryChallan.Create(
                req.ChallanNumber,
                req.CustomerId,
                req.ProjectId,
                req.SiteAddress,
                req.TransporterName,
                req.VehicleNumber,
                req.DriverName,
                req.DriverPhone,
                req.CustomerOtp,
                req.Notes);

            if (req.Items != null)
            {
                foreach (var item in req.Items)
                {
                    challan.AddItem(item.ProductId, item.ItemDescription, item.Quantity, item.Unit);
                }
            }

            dbContext.DeliveryChallans.Add(challan);
            await unitOfWork.SaveChangesAsync();
            return Results.Created($"/api/fulfillment/challans/{challan.Id}", challan);
        })
        .WithName("CreateDeliveryChallan");

        // Confirm delivery with OTP
        challanGroup.MapPost("/{id:guid}/confirm", async (Guid id, [FromBody] ConfirmChallanDeliveryRequest req, IApplicationDbContext dbContext, IUnitOfWork unitOfWork) =>
        {
            var challan = await dbContext.DeliveryChallans.FindAsync(id);
            if (challan is null) return Results.NotFound();

            challan.ConfirmDelivery(req.EnteredOtp, req.ProofOfDeliveryUrl);
            await unitOfWork.SaveChangesAsync();
            return Results.Ok(challan);
        })
        .WithName("ConfirmDeliveryChallan");

        var grnGroup = app.MapGroup("/api/stores/grn")
            .WithTags("Stores - Goods Receipt Notes")
            .RequireAuthorization();

        // Get GRNs
        grnGroup.MapGet("/", async ([FromQuery] GrnStatus? status, IApplicationDbContext dbContext) =>
        {
            var query = dbContext.GoodsReceiptNotes.Include(x => x.Items).AsNoTracking();
            if (status.HasValue)
            {
                query = query.Where(x => x.Status == status.Value);
            }
            var list = await query.OrderByDescending(x => x.ReceivedDate).ToListAsync();
            return Results.Ok(list);
        })
        .WithName("GetGoodsReceiptNotes");

        // Create GRN
        grnGroup.MapPost("/", async (CreateGrnRequest req, IApplicationDbContext dbContext, IUnitOfWork unitOfWork) =>
        {
            var grn = GoodsReceiptNote.Create(
                req.GrnNumber,
                req.SupplierId,
                req.PurchaseOrderId,
                req.StoreLocation,
                req.VehicleNumber,
                req.DeliveryChallanNumber,
                req.Remarks);

            if (req.Items != null)
            {
                foreach (var item in req.Items)
                {
                    grn.AddItem(
                        item.ProductId,
                        item.ItemDescription,
                        item.ReceivedQuantity,
                        item.AcceptedQuantity,
                        item.RejectedQuantity,
                        item.Unit,
                        item.RejectionReason);
                }
            }

            dbContext.GoodsReceiptNotes.Add(grn);
            await unitOfWork.SaveChangesAsync();
            return Results.Created($"/api/stores/grn/{grn.Id}", grn);
        })
        .WithName("CreateGoodsReceiptNote");
    }
}

public record CreateChallanRequest(
    string ChallanNumber,
    Guid CustomerId,
    Guid? ProjectId,
    string? SiteAddress,
    string? TransporterName,
    string? VehicleNumber,
    string? DriverName,
    string? DriverPhone,
    string? CustomerOtp,
    string? Notes,
    List<CreateChallanItemRequest>? Items);

public record CreateChallanItemRequest(
    Guid ProductId,
    string ItemDescription,
    decimal Quantity,
    string Unit);

public record ConfirmChallanDeliveryRequest(string? EnteredOtp, string? ProofOfDeliveryUrl);

public record CreateGrnRequest(
    string GrnNumber,
    Guid SupplierId,
    Guid? PurchaseOrderId,
    string? StoreLocation,
    string? VehicleNumber,
    string? DeliveryChallanNumber,
    string? Remarks,
    List<CreateGrnItemRequest>? Items);

public record CreateGrnItemRequest(
    Guid ProductId,
    string ItemDescription,
    decimal ReceivedQuantity,
    decimal AcceptedQuantity,
    decimal RejectedQuantity,
    string Unit,
    string? RejectionReason);
