using NOIR.Application.Features.Stock.Commands.AddStockTransferItem;
using NOIR.Application.Features.Stock.Commands.ApproveStockTransfer;
using NOIR.Application.Features.Stock.Commands.CancelStockTransfer;
using NOIR.Application.Features.Stock.Commands.CompleteStockTransfer;
using NOIR.Application.Features.Stock.Commands.CreateStockTransfer;
using NOIR.Application.Features.Stock.Commands.RejectStockTransfer;
using NOIR.Application.Features.Stock.Commands.RemoveStockTransferItem;
using NOIR.Application.Features.Stock.Commands.SubmitStockTransfer;
using NOIR.Application.Features.Stock.Commands.UpdateStockTransfer;
using NOIR.Application.Features.Stock.DTOs;
using NOIR.Application.Features.Stock.Queries.GetStockTransferById;
using NOIR.Application.Features.Stock.Queries.GetStockTransfers;
using NOIR.Domain.Entities.Stock;

namespace NOIR.Web.Endpoints;

/// <summary>
/// Stock transfer API endpoints.
/// </summary>
public static class StockTransferEndpoints
{
    public static void MapStockTransferEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/stock/transfers")
            .WithTags("Stock Transfers")
            .RequireFeature(ModuleNames.Erp.StockSub.Transfers)
            .RequireAuthorization();

        // Get transfers (paginated)
        group.MapGet("/", async (
            [FromQuery] string? search,
            [FromQuery] TransferStatus? status,
            [FromQuery] string? fromLocation,
            [FromQuery] string? toLocation,
            [FromQuery] int? page,
            [FromQuery] int? pageSize,
            IMessageBus bus) =>
        {
            var query = new GetStockTransfersQuery(search, status, fromLocation, toLocation, page ?? 1, pageSize ?? 20);
            var result = await bus.InvokeAsync<Result<PagedResult<StockTransferListDto>>>(query);
            return result.ToHttpResult();
        })
        .RequireAuthorization(Permissions.StockTransfersRead)
        .WithName("GetStockTransfers")
        .WithSummary("Get paginated stock transfers")
        .WithDescription("Returns stock transfers with optional filtering by status, locations, and search term.")
        .Produces<PagedResult<StockTransferListDto>>(StatusCodes.Status200OK);

        // Get transfer by ID
        group.MapGet("/{id:guid}", async (Guid id, IMessageBus bus) =>
        {
            var query = new GetStockTransferByIdQuery(id);
            var result = await bus.InvokeAsync<Result<StockTransferDto>>(query);
            return result.ToHttpResult();
        })
        .RequireAuthorization(Permissions.StockTransfersRead)
        .WithName("GetStockTransferById")
        .WithSummary("Get stock transfer by ID")
        .WithDescription("Returns full stock transfer details including items with product names.")
        .Produces<StockTransferDto>(StatusCodes.Status200OK)
        .Produces<ProblemDetails>(StatusCodes.Status404NotFound);

        // Create transfer
        group.MapPost("/", async (
            CreateStockTransferRequest request,
            [FromServices] ICurrentUser currentUser,
            IMessageBus bus) =>
        {
            var command = new CreateStockTransferCommand(
                request.TransferNumber,
                request.FromLocation,
                request.ToLocation,
                request.TransferDate,
                request.Notes,
                request.Items) with { UserId = currentUser.UserId };
            var result = await bus.InvokeAsync<Result<StockTransferDto>>(command);
            return result.ToHttpResult();
        })
        .RequireAuthorization(Permissions.StockTransfersCreate)
        .WithName("CreateStockTransfer")
        .WithSummary("Create stock transfer")
        .WithDescription("Creates a new stock transfer in Draft status with items.")
        .Produces<StockTransferDto>(StatusCodes.Status200OK)
        .Produces<ProblemDetails>(StatusCodes.Status400BadRequest);

        // Update transfer header
        group.MapPut("/{id:guid}", async (
            Guid id,
            UpdateStockTransferRequest request,
            [FromServices] ICurrentUser currentUser,
            IMessageBus bus) =>
        {
            var command = new UpdateStockTransferCommand(
                id,
                request.FromLocation,
                request.ToLocation,
                request.TransferDate,
                request.Notes) { UserId = currentUser.UserId };
            var result = await bus.InvokeAsync<Result<StockTransferDto>>(command);
            return result.ToHttpResult();
        })
        .RequireAuthorization(Permissions.StockTransfersCreate)
        .WithName("UpdateStockTransfer")
        .WithSummary("Update stock transfer")
        .WithDescription("Updates the header of a Draft stock transfer.")
        .Produces<StockTransferDto>(StatusCodes.Status200OK)
        .Produces<ProblemDetails>(StatusCodes.Status404NotFound);

        // Add transfer item
        group.MapPost("/{id:guid}/items", async (
            Guid id,
            AddStockTransferItemRequest request,
            [FromServices] ICurrentUser currentUser,
            IMessageBus bus) =>
        {
            var command = new AddStockTransferItemCommand(
                id,
                request.ProductId,
                request.Quantity,
                request.Unit,
                request.Remarks) { UserId = currentUser.UserId };
            var result = await bus.InvokeAsync<Result<StockTransferDto>>(command);
            return result.ToHttpResult();
        })
        .RequireAuthorization(Permissions.StockTransfersCreate)
        .WithName("AddStockTransferItem")
        .WithSummary("Add stock transfer item")
        .WithDescription("Adds a product line to a Draft stock transfer.")
        .Produces<StockTransferDto>(StatusCodes.Status200OK)
        .Produces<ProblemDetails>(StatusCodes.Status404NotFound);

        // Remove transfer item
        group.MapDelete("/{id:guid}/items/{itemId:guid}", async (
            Guid id,
            Guid itemId,
            [FromServices] ICurrentUser currentUser,
            IMessageBus bus) =>
        {
            var command = new RemoveStockTransferItemCommand(id, itemId) { UserId = currentUser.UserId };
            var result = await bus.InvokeAsync<Result<StockTransferDto>>(command);
            return result.ToHttpResult();
        })
        .RequireAuthorization(Permissions.StockTransfersCreate)
        .WithName("RemoveStockTransferItem")
        .WithSummary("Remove stock transfer item")
        .WithDescription("Removes a product line from a Draft stock transfer.")
        .Produces<StockTransferDto>(StatusCodes.Status200OK)
        .Produces<ProblemDetails>(StatusCodes.Status404NotFound);

        // Submit transfer
        group.MapPost("/{id:guid}/submit", async (
            Guid id,
            [FromServices] ICurrentUser currentUser,
            IMessageBus bus) =>
        {
            var command = new SubmitStockTransferCommand(id) { UserId = currentUser.UserId };
            var result = await bus.InvokeAsync<Result<StockTransferDto>>(command);
            return result.ToHttpResult();
        })
        .RequireAuthorization(Permissions.StockTransfersCreate)
        .WithName("SubmitStockTransfer")
        .WithSummary("Submit stock transfer")
        .WithDescription("Moves a Draft stock transfer to Submitted for approval.")
        .Produces<StockTransferDto>(StatusCodes.Status200OK)
        .Produces<ProblemDetails>(StatusCodes.Status404NotFound);

        // Approve transfer
        group.MapPost("/{id:guid}/approve", async (
            Guid id,
            [FromServices] ICurrentUser currentUser,
            IMessageBus bus) =>
        {
            var command = new ApproveStockTransferCommand(id) { UserId = currentUser.UserId };
            var result = await bus.InvokeAsync<Result<StockTransferDto>>(command);
            return result.ToHttpResult();
        })
        .RequireAuthorization(Permissions.StockTransfersApprove)
        .WithName("ApproveStockTransfer")
        .WithSummary("Approve stock transfer")
        .WithDescription("Approves a Draft or Submitted stock transfer.")
        .Produces<StockTransferDto>(StatusCodes.Status200OK)
        .Produces<ProblemDetails>(StatusCodes.Status404NotFound);

        // Reject transfer
        group.MapPost("/{id:guid}/reject", async (
            Guid id,
            RejectStockTransferRequest request,
            [FromServices] ICurrentUser currentUser,
            IMessageBus bus) =>
        {
            var command = new RejectStockTransferCommand(id, request.Reason) { UserId = currentUser.UserId };
            var result = await bus.InvokeAsync<Result<StockTransferDto>>(command);
            return result.ToHttpResult();
        })
        .RequireAuthorization(Permissions.StockTransfersApprove)
        .WithName("RejectStockTransfer")
        .WithSummary("Reject stock transfer")
        .WithDescription("Rejects a Submitted stock transfer with an optional reason.")
        .Produces<StockTransferDto>(StatusCodes.Status200OK)
        .Produces<ProblemDetails>(StatusCodes.Status404NotFound);

        // Cancel transfer
        group.MapPost("/{id:guid}/cancel", async (
            Guid id,
            [FromServices] ICurrentUser currentUser,
            IMessageBus bus) =>
        {
            var command = new CancelStockTransferCommand(id) { UserId = currentUser.UserId };
            var result = await bus.InvokeAsync<Result<StockTransferDto>>(command);
            return result.ToHttpResult();
        })
        .RequireAuthorization(Permissions.StockTransfersCreate)
        .WithName("CancelStockTransfer")
        .WithSummary("Cancel stock transfer")
        .WithDescription("Cancels a Draft or Submitted stock transfer.")
        .Produces<StockTransferDto>(StatusCodes.Status200OK)
        .Produces<ProblemDetails>(StatusCodes.Status404NotFound);

        // Complete transfer
        group.MapPost("/{id:guid}/complete", async (
            Guid id,
            [FromServices] ICurrentUser currentUser,
            IMessageBus bus) =>
        {
            var command = new CompleteStockTransferCommand(id) { UserId = currentUser.UserId };
            var result = await bus.InvokeAsync<Result<StockTransferDto>>(command);
            return result.ToHttpResult();
        })
        .RequireAuthorization(Permissions.StockTransfersApprove)
        .WithName("CompleteStockTransfer")
        .WithSummary("Complete stock transfer")
        .WithDescription("Completes an Approved stock transfer, marking the stock movement as executed.")
        .Produces<StockTransferDto>(StatusCodes.Status200OK)
        .Produces<ProblemDetails>(StatusCodes.Status404NotFound);
    }
}
