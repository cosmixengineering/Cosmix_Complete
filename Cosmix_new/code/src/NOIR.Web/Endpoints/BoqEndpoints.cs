using NOIR.Application.Features.Estimation.Commands.AddBoqItem;
using NOIR.Application.Features.Estimation.Commands.ApproveBoq;
using NOIR.Application.Features.Estimation.Commands.CreateBoq;
using NOIR.Application.Features.Estimation.Commands.DeleteBoq;
using NOIR.Application.Features.Estimation.Commands.DeleteBoqItem;
using NOIR.Application.Features.Estimation.Commands.RejectBoq;
using NOIR.Application.Features.Estimation.Commands.SubmitBoq;
using NOIR.Application.Features.Estimation.Commands.UpdateBoq;
using NOIR.Application.Features.Estimation.Commands.UpdateBoqItem;
using NOIR.Application.Features.Estimation.DTOs;
using NOIR.Application.Features.Estimation.Queries.GetBoqById;
using NOIR.Application.Features.Estimation.Queries.GetBoqs;
using NOIR.Domain.Entities.Estimation;

namespace NOIR.Web.Endpoints;

/// <summary>
/// Estimation BOQ API endpoints.
/// Provides CRUD, line item management, and approval workflow for Bills of Quantities.
/// </summary>
public static class BoqEndpoints
{
    public static void MapBoqEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/estimation/boqs")
            .WithTags("Estimation - BOQ")
            .RequireFeature(ModuleNames.Erp.Estimation)
            .RequireAuthorization();

        // Get all BOQs (paged)
        group.MapGet("/", async (
            [FromQuery] string? search,
            [FromQuery] BoqStatus? status,
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 20,
            IMessageBus bus = null!) =>
        {
            var query = new GetBoqsQuery(search, status, pageNumber, pageSize);
            var result = await bus.InvokeAsync<Result<PagedResult<BoqListDto>>>(query);
            return result.ToHttpResult();
        })
        .RequireAuthorization(Permissions.EstimationBoqsRead)
        .WithName("GetBoqs")
        .WithSummary("Get list of BOQs")
        .WithDescription("Returns paged list of BOQs with optional filtering.")
        .Produces<PagedResult<BoqListDto>>(StatusCodes.Status200OK);

        // Get BOQ by ID
        group.MapGet("/{id:guid}", async (Guid id, IMessageBus bus) =>
        {
            var query = new GetBoqByIdQuery(id);
            var result = await bus.InvokeAsync<Result<BoqDto>>(query);
            return result.ToHttpResult();
        })
        .RequireAuthorization(Permissions.EstimationBoqsRead)
        .WithName("GetBoqById")
        .WithSummary("Get BOQ by ID")
        .WithDescription("Returns BOQ details by ID, including line items and revisions.")
        .Produces<BoqDto>(StatusCodes.Status200OK)
        .Produces<ProblemDetails>(StatusCodes.Status404NotFound);

        // Create BOQ
        group.MapPost("/", async (
            CreateBoqRequest request,
            [FromServices] ICurrentUser currentUser,
            IMessageBus bus) =>
        {
            var command = new CreateBoqCommand(
                request.BoqNumber,
                request.Title,
                request.CustomerId,
                request.ProjectId,
                request.Currency,
                request.Description,
                request.Notes,
                request.MarkupPercent)
            {
                UserId = currentUser.UserId
            };
            var result = await bus.InvokeAsync<Result<BoqDto>>(command);
            return result.ToHttpResult();
        })
        .RequireAuthorization(Permissions.EstimationBoqsCreate)
        .WithName("CreateBoq")
        .WithSummary("Create a new BOQ")
        .WithDescription("Creates a new bill of quantities in Draft status.")
        .Produces<BoqDto>(StatusCodes.Status200OK)
        .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
        .Produces<ProblemDetails>(StatusCodes.Status409Conflict);

        // Update BOQ header
        group.MapPut("/{id:guid}", async (
            Guid id,
            UpdateBoqRequest request,
            [FromServices] ICurrentUser currentUser,
            IMessageBus bus) =>
        {
            var command = new UpdateBoqCommand(
                id,
                request.Title,
                request.Description,
                request.CustomerId,
                request.ProjectId,
                request.MarkupPercent,
                request.Notes)
            {
                UserId = currentUser.UserId
            };
            var result = await bus.InvokeAsync<Result<BoqDto>>(command);
            return result.ToHttpResult();
        })
        .RequireAuthorization(Permissions.EstimationBoqsUpdate)
        .WithName("UpdateBoq")
        .WithSummary("Update a BOQ")
        .WithDescription("Updates BOQ header details and recalculates totals.")
        .Produces<BoqDto>(StatusCodes.Status200OK)
        .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
        .Produces<ProblemDetails>(StatusCodes.Status404NotFound);

        // Delete BOQ (soft delete, draft only)
        group.MapDelete("/{id:guid}", async (
            Guid id,
            [FromServices] ICurrentUser currentUser,
            IMessageBus bus) =>
        {
            var command = new DeleteBoqCommand(id) { UserId = currentUser.UserId };
            var result = await bus.InvokeAsync<Result<bool>>(command);
            return result.ToHttpResult();
        })
        .RequireAuthorization(Permissions.EstimationBoqsDelete)
        .WithName("DeleteBoq")
        .WithSummary("Soft-delete a BOQ")
        .WithDescription("Soft-deletes a BOQ. Will fail if it is not in Draft status.")
        .Produces(StatusCodes.Status200OK)
        .Produces<ProblemDetails>(StatusCodes.Status404NotFound)
        .Produces<ProblemDetails>(StatusCodes.Status409Conflict);

        // Submit BOQ for review
        group.MapPost("/{id:guid}/submit", async (
            Guid id,
            [FromServices] ICurrentUser currentUser,
            IMessageBus bus) =>
        {
            var command = new SubmitBoqCommand(id) { UserId = currentUser.UserId };
            var result = await bus.InvokeAsync<Result<BoqDto>>(command);
            return result.ToHttpResult();
        })
        .RequireAuthorization(Permissions.EstimationBoqsUpdate)
        .WithName("SubmitBoq")
        .WithSummary("Submit BOQ for review")
        .WithDescription("Moves a Draft BOQ to UnderReview status.")
        .Produces<BoqDto>(StatusCodes.Status200OK)
        .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
        .Produces<ProblemDetails>(StatusCodes.Status404NotFound);

        // Approve BOQ
        group.MapPost("/{id:guid}/approve", async (
            Guid id,
            [FromServices] ICurrentUser currentUser,
            IMessageBus bus) =>
        {
            var command = new ApproveBoqCommand(id) { UserId = currentUser.UserId };
            var result = await bus.InvokeAsync<Result<BoqDto>>(command);
            return result.ToHttpResult();
        })
        .RequireAuthorization(Permissions.EstimationBoqsApprove)
        .WithName("ApproveBoq")
        .WithSummary("Approve a BOQ")
        .WithDescription("Approves a BOQ under review and captures a revision snapshot.")
        .Produces<BoqDto>(StatusCodes.Status200OK)
        .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
        .Produces<ProblemDetails>(StatusCodes.Status404NotFound);

        // Reject BOQ
        group.MapPost("/{id:guid}/reject", async (
            Guid id,
            RejectBoqRequest request,
            [FromServices] ICurrentUser currentUser,
            IMessageBus bus) =>
        {
            var command = new RejectBoqCommand(id, request.Reason) { UserId = currentUser.UserId };
            var result = await bus.InvokeAsync<Result<BoqDto>>(command);
            return result.ToHttpResult();
        })
        .RequireAuthorization(Permissions.EstimationBoqsApprove)
        .WithName("RejectBoq")
        .WithSummary("Reject a BOQ")
        .WithDescription("Rejects a BOQ under review with an optional reason.")
        .Produces<BoqDto>(StatusCodes.Status200OK)
        .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
        .Produces<ProblemDetails>(StatusCodes.Status404NotFound);

        // Add BOQ item
        group.MapPost("/{id:guid}/items", async (
            Guid id,
            AddBoqItemRequest request,
            [FromServices] ICurrentUser currentUser,
            IMessageBus bus) =>
        {
            var command = new AddBoqItemCommand(
                id,
                request.Section,
                request.ItemCode,
                request.Description,
                request.Quantity,
                request.Unit,
                request.UnitRate,
                request.SortOrder)
            {
                UserId = currentUser.UserId
            };
            var result = await bus.InvokeAsync<Result<BoqItemDto>>(command);
            return result.ToHttpResult();
        })
        .RequireAuthorization(Permissions.EstimationBoqsCreate)
        .WithName("AddBoqItem")
        .WithSummary("Add a BOQ line item")
        .WithDescription("Adds a line item to a BOQ and recalculates totals.")
        .Produces<BoqItemDto>(StatusCodes.Status200OK)
        .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
        .Produces<ProblemDetails>(StatusCodes.Status404NotFound);

        // Update BOQ item
        group.MapPut("/{id:guid}/items/{itemId:guid}", async (
            Guid id,
            Guid itemId,
            UpdateBoqItemRequest request,
            [FromServices] ICurrentUser currentUser,
            IMessageBus bus) =>
        {
            var command = new UpdateBoqItemCommand(
                id,
                itemId,
                request.Section,
                request.ItemCode,
                request.Description,
                request.Quantity,
                request.Unit,
                request.UnitRate,
                request.SortOrder)
            {
                UserId = currentUser.UserId
            };
            var result = await bus.InvokeAsync<Result<BoqItemDto>>(command);
            return result.ToHttpResult();
        })
        .RequireAuthorization(Permissions.EstimationBoqsUpdate)
        .WithName("UpdateBoqItem")
        .WithSummary("Update a BOQ line item")
        .WithDescription("Updates a BOQ line item and recalculates totals.")
        .Produces<BoqItemDto>(StatusCodes.Status200OK)
        .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
        .Produces<ProblemDetails>(StatusCodes.Status404NotFound);

        // Delete BOQ item
        group.MapDelete("/{id:guid}/items/{itemId:guid}", async (
            Guid id,
            Guid itemId,
            [FromServices] ICurrentUser currentUser,
            IMessageBus bus) =>
        {
            var command = new DeleteBoqItemCommand(id, itemId) { UserId = currentUser.UserId };
            var result = await bus.InvokeAsync<Result<bool>>(command);
            return result.ToHttpResult();
        })
        .RequireAuthorization(Permissions.EstimationBoqsUpdate)
        .WithName("DeleteBoqItem")
        .WithSummary("Remove a BOQ line item")
        .WithDescription("Removes a line item from a BOQ and recalculates totals.")
        .Produces(StatusCodes.Status200OK)
        .Produces<ProblemDetails>(StatusCodes.Status404NotFound);
    }
}

/// <summary>
/// Request to reject a BOQ.
/// </summary>
public sealed record RejectBoqRequest(string? Reason = null);
