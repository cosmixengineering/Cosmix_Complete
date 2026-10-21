using NOIR.Application.Features.Stock.Commands.AddStockIndentItem;
using NOIR.Application.Features.Stock.Commands.ApproveStockIndent;
using NOIR.Application.Features.Stock.Commands.CancelStockIndent;
using NOIR.Application.Features.Stock.Commands.CreateStockIndent;
using NOIR.Application.Features.Stock.Commands.RejectStockIndent;
using NOIR.Application.Features.Stock.Commands.RemoveStockIndentItem;
using NOIR.Application.Features.Stock.Commands.SubmitStockIndent;
using NOIR.Application.Features.Stock.Commands.UpdateStockIndent;
using NOIR.Application.Features.Stock.DTOs;
using NOIR.Application.Features.Stock.Queries.GetStockIndentById;
using NOIR.Application.Features.Stock.Queries.GetStockIndents;
using NOIR.Domain.Entities.Stock;

namespace NOIR.Web.Endpoints;

/// <summary>
/// Stock indent (purchase requisition) API endpoints.
/// </summary>
public static class StockIndentEndpoints
{
    public static void MapStockIndentEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/stock/indents")
            .WithTags("Stock Indents")
            .RequireFeature(ModuleNames.Erp.StockSub.Indents)
            .RequireAuthorization();

        // Get indents (paginated)
        group.MapGet("/", async (
            [FromQuery] string? search,
            [FromQuery] IndentStatus? status,
            [FromQuery] int? page,
            [FromQuery] int? pageSize,
            IMessageBus bus) =>
        {
            var query = new GetStockIndentsQuery(search, status, page ?? 1, pageSize ?? 20);
            var result = await bus.InvokeAsync<Result<PagedResult<StockIndentListDto>>>(query);
            return result.ToHttpResult();
        })
        .RequireAuthorization(Permissions.StockIndentsRead)
        .WithName("GetStockIndents")
        .WithSummary("Get paginated stock indents")
        .WithDescription("Returns stock indents with optional filtering by status and search term.")
        .Produces<PagedResult<StockIndentListDto>>(StatusCodes.Status200OK);

        // Get indent by ID
        group.MapGet("/{id:guid}", async (Guid id, IMessageBus bus) =>
        {
            var query = new GetStockIndentByIdQuery(id);
            var result = await bus.InvokeAsync<Result<StockIndentDto>>(query);
            return result.ToHttpResult();
        })
        .RequireAuthorization(Permissions.StockIndentsRead)
        .WithName("GetStockIndentById")
        .WithSummary("Get stock indent by ID")
        .WithDescription("Returns full stock indent details including items with product names.")
        .Produces<StockIndentDto>(StatusCodes.Status200OK)
        .Produces<ProblemDetails>(StatusCodes.Status404NotFound);

        // Create indent
        group.MapPost("/", async (
            CreateStockIndentRequest request,
            [FromServices] ICurrentUser currentUser,
            IMessageBus bus) =>
        {
            var command = new CreateStockIndentCommand(
                request.IndentNumber,
                request.NeededByDate,
                request.Department,
                request.Notes,
                request.Items) with { UserId = currentUser.UserId };
            var result = await bus.InvokeAsync<Result<StockIndentDto>>(command);
            return result.ToHttpResult();
        })
        .RequireAuthorization(Permissions.StockIndentsCreate)
        .WithName("CreateStockIndent")
        .WithSummary("Create stock indent")
        .WithDescription("Creates a new stock indent in Draft status with items.")
        .Produces<StockIndentDto>(StatusCodes.Status200OK)
        .Produces<ProblemDetails>(StatusCodes.Status400BadRequest);

        // Update indent header
        group.MapPut("/{id:guid}", async (
            Guid id,
            UpdateStockIndentRequest request,
            [FromServices] ICurrentUser currentUser,
            IMessageBus bus) =>
        {
            var command = new UpdateStockIndentCommand(
                id,
                request.NeededByDate,
                request.Department,
                request.Notes) { UserId = currentUser.UserId };
            var result = await bus.InvokeAsync<Result<StockIndentDto>>(command);
            return result.ToHttpResult();
        })
        .RequireAuthorization(Permissions.StockIndentsCreate)
        .WithName("UpdateStockIndent")
        .WithSummary("Update stock indent")
        .WithDescription("Updates the header of a Draft stock indent.")
        .Produces<StockIndentDto>(StatusCodes.Status200OK)
        .Produces<ProblemDetails>(StatusCodes.Status404NotFound);

        // Add indent item
        group.MapPost("/{id:guid}/items", async (
            Guid id,
            AddStockIndentItemRequest request,
            [FromServices] ICurrentUser currentUser,
            IMessageBus bus) =>
        {
            var command = new AddStockIndentItemCommand(
                id,
                request.ProductId,
                request.Quantity,
                request.Unit,
                request.Remarks) { UserId = currentUser.UserId };
            var result = await bus.InvokeAsync<Result<StockIndentDto>>(command);
            return result.ToHttpResult();
        })
        .RequireAuthorization(Permissions.StockIndentsCreate)
        .WithName("AddStockIndentItem")
        .WithSummary("Add stock indent item")
        .WithDescription("Adds a product line to a Draft stock indent.")
        .Produces<StockIndentDto>(StatusCodes.Status200OK)
        .Produces<ProblemDetails>(StatusCodes.Status404NotFound);

        // Remove indent item
        group.MapDelete("/{id:guid}/items/{itemId:guid}", async (
            Guid id,
            Guid itemId,
            [FromServices] ICurrentUser currentUser,
            IMessageBus bus) =>
        {
            var command = new RemoveStockIndentItemCommand(id, itemId) { UserId = currentUser.UserId };
            var result = await bus.InvokeAsync<Result<StockIndentDto>>(command);
            return result.ToHttpResult();
        })
        .RequireAuthorization(Permissions.StockIndentsCreate)
        .WithName("RemoveStockIndentItem")
        .WithSummary("Remove stock indent item")
        .WithDescription("Removes a product line from a Draft stock indent.")
        .Produces<StockIndentDto>(StatusCodes.Status200OK)
        .Produces<ProblemDetails>(StatusCodes.Status404NotFound);

        // Submit indent
        group.MapPost("/{id:guid}/submit", async (
            Guid id,
            [FromServices] ICurrentUser currentUser,
            IMessageBus bus) =>
        {
            var command = new SubmitStockIndentCommand(id) { UserId = currentUser.UserId };
            var result = await bus.InvokeAsync<Result<StockIndentDto>>(command);
            return result.ToHttpResult();
        })
        .RequireAuthorization(Permissions.StockIndentsCreate)
        .WithName("SubmitStockIndent")
        .WithSummary("Submit stock indent")
        .WithDescription("Moves a Draft stock indent to Submitted for approval.")
        .Produces<StockIndentDto>(StatusCodes.Status200OK)
        .Produces<ProblemDetails>(StatusCodes.Status404NotFound);

        // Approve indent
        group.MapPost("/{id:guid}/approve", async (
            Guid id,
            [FromServices] ICurrentUser currentUser,
            IMessageBus bus) =>
        {
            var command = new ApproveStockIndentCommand(id) { UserId = currentUser.UserId };
            var result = await bus.InvokeAsync<Result<StockIndentDto>>(command);
            return result.ToHttpResult();
        })
        .RequireAuthorization(Permissions.StockIndentsApprove)
        .WithName("ApproveStockIndent")
        .WithSummary("Approve stock indent")
        .WithDescription("Approves a Draft or Submitted stock indent.")
        .Produces<StockIndentDto>(StatusCodes.Status200OK)
        .Produces<ProblemDetails>(StatusCodes.Status404NotFound);

        // Reject indent
        group.MapPost("/{id:guid}/reject", async (
            Guid id,
            RejectStockIndentRequest request,
            [FromServices] ICurrentUser currentUser,
            IMessageBus bus) =>
        {
            var command = new RejectStockIndentCommand(id, request.Reason) { UserId = currentUser.UserId };
            var result = await bus.InvokeAsync<Result<StockIndentDto>>(command);
            return result.ToHttpResult();
        })
        .RequireAuthorization(Permissions.StockIndentsApprove)
        .WithName("RejectStockIndent")
        .WithSummary("Reject stock indent")
        .WithDescription("Rejects a Submitted stock indent with an optional reason.")
        .Produces<StockIndentDto>(StatusCodes.Status200OK)
        .Produces<ProblemDetails>(StatusCodes.Status404NotFound);

        // Cancel indent
        group.MapPost("/{id:guid}/cancel", async (
            Guid id,
            [FromServices] ICurrentUser currentUser,
            IMessageBus bus) =>
        {
            var command = new CancelStockIndentCommand(id) { UserId = currentUser.UserId };
            var result = await bus.InvokeAsync<Result<StockIndentDto>>(command);
            return result.ToHttpResult();
        })
        .RequireAuthorization(Permissions.StockIndentsCreate)
        .WithName("CancelStockIndent")
        .WithSummary("Cancel stock indent")
        .WithDescription("Cancels a Draft or Submitted stock indent.")
        .Produces<StockIndentDto>(StatusCodes.Status200OK)
        .Produces<ProblemDetails>(StatusCodes.Status404NotFound);
    }
}
