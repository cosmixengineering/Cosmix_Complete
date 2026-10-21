using NOIR.Application.Features.Manufacturing.Commands.CancelProductionOrder;
using NOIR.Application.Features.Manufacturing.Commands.CompleteProductionOrder;
using NOIR.Application.Features.Manufacturing.Commands.CreateProductionOrder;
using NOIR.Application.Features.Manufacturing.Commands.IssueMaterial;
using NOIR.Application.Features.Manufacturing.Commands.ReleaseProductionOrder;
using NOIR.Application.Features.Manufacturing.Commands.ReportProductionProgress;
using NOIR.Application.Features.Manufacturing.Commands.StartProductionOrder;
using NOIR.Application.Features.Manufacturing.Commands.UpdateProductionOrder;
using NOIR.Application.Features.Manufacturing.DTOs;
using NOIR.Application.Features.Manufacturing.Queries.GetProductionOrderById;
using NOIR.Application.Features.Manufacturing.Queries.GetProductionOrders;
using NOIR.Domain.Entities.Manufacturing;

namespace NOIR.Web.Endpoints;

/// <summary>
/// Manufacturing production order API endpoints.
/// Provides creation, lifecycle workflow, progress reporting, and material issue.
/// </summary>
public static class ProductionOrderEndpoints
{
    public static void MapProductionOrderEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/manufacturing/production-orders")
            .WithTags("Manufacturing - Production Orders")
            .RequireFeature(ModuleNames.Erp.ManufacturingSub.ProductionOrders)
            .RequireAuthorization();

        // Get all production orders (paged)
        group.MapGet("/", async (
            [FromQuery] string? search,
            [FromQuery] ProductionOrderStatus? status,
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 20,
            IMessageBus bus = null!) =>
        {
            var query = new GetProductionOrdersQuery(search, status, pageNumber, pageSize);
            var result = await bus.InvokeAsync<Result<PagedResult<ProductionOrderListDto>>>(query);
            return result.ToHttpResult();
        })
        .RequireAuthorization(Permissions.ManufacturingOrdersRead)
        .WithName("GetProductionOrders")
        .WithSummary("Get list of production orders")
        .WithDescription("Returns paged list of production orders with optional filtering.")
        .Produces<PagedResult<ProductionOrderListDto>>(StatusCodes.Status200OK);

        // Get production order by ID
        group.MapGet("/{id:guid}", async (Guid id, IMessageBus bus) =>
        {
            var query = new GetProductionOrderByIdQuery(id);
            var result = await bus.InvokeAsync<Result<ProductionOrderDto>>(query);
            return result.ToHttpResult();
        })
        .RequireAuthorization(Permissions.ManufacturingOrdersRead)
        .WithName("GetProductionOrderById")
        .WithSummary("Get production order by ID")
        .WithDescription("Returns production order details by ID, including material requirements.")
        .Produces<ProductionOrderDto>(StatusCodes.Status200OK)
        .Produces<ProblemDetails>(StatusCodes.Status404NotFound);

        // Create production order
        group.MapPost("/", async (
            CreateProductionOrderRequest request,
            [FromServices] ICurrentUser currentUser,
            IMessageBus bus) =>
        {
            var command = new CreateProductionOrderCommand(
                request.OrderNumber,
                request.BillOfMaterialId,
                request.PlannedQuantity,
                request.PlannedStartDate,
                request.PlannedEndDate,
                request.Notes)
            {
                UserId = currentUser.UserId
            };
            var result = await bus.InvokeAsync<Result<ProductionOrderDto>>(command);
            return result.ToHttpResult();
        })
        .RequireAuthorization(Permissions.ManufacturingOrdersCreate)
        .WithName("CreateProductionOrder")
        .WithSummary("Create a new production order")
        .WithDescription("Creates a draft production order with material requirements snapshotted from the BOM.")
        .Produces<ProductionOrderDto>(StatusCodes.Status200OK)
        .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
        .Produces<ProblemDetails>(StatusCodes.Status409Conflict);

        // Update production order (draft only)
        group.MapPut("/{id:guid}", async (
            Guid id,
            UpdateProductionOrderRequest request,
            [FromServices] ICurrentUser currentUser,
            IMessageBus bus) =>
        {
            var command = new UpdateProductionOrderCommand(
                id,
                request.PlannedQuantity,
                request.PlannedStartDate,
                request.PlannedEndDate,
                request.Notes)
            {
                UserId = currentUser.UserId
            };
            var result = await bus.InvokeAsync<Result<ProductionOrderDto>>(command);
            return result.ToHttpResult();
        })
        .RequireAuthorization(Permissions.ManufacturingOrdersUpdate)
        .WithName("UpdateProductionOrder")
        .WithSummary("Update a production order")
        .WithDescription("Updates a draft production order header; rescales the snapshot when planned quantity changes.")
        .Produces<ProductionOrderDto>(StatusCodes.Status200OK)
        .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
        .Produces<ProblemDetails>(StatusCodes.Status404NotFound)
        .Produces<ProblemDetails>(StatusCodes.Status409Conflict);

        // Release production order
        group.MapPost("/{id:guid}/release", async (
            Guid id,
            [FromServices] ICurrentUser currentUser,
            IMessageBus bus) =>
        {
            var command = new ReleaseProductionOrderCommand(id) { UserId = currentUser.UserId };
            var result = await bus.InvokeAsync<Result<ProductionOrderDto>>(command);
            return result.ToHttpResult();
        })
        .RequireAuthorization(Permissions.ManufacturingOrdersUpdate)
        .WithName("ReleaseProductionOrder")
        .WithSummary("Release a production order")
        .WithDescription("Moves a draft production order to Released.")
        .Produces<ProductionOrderDto>(StatusCodes.Status200OK)
        .Produces<ProblemDetails>(StatusCodes.Status404NotFound)
        .Produces<ProblemDetails>(StatusCodes.Status409Conflict);

        // Start production order
        group.MapPost("/{id:guid}/start", async (
            Guid id,
            [FromServices] ICurrentUser currentUser,
            IMessageBus bus) =>
        {
            var command = new StartProductionOrderCommand(id) { UserId = currentUser.UserId };
            var result = await bus.InvokeAsync<Result<ProductionOrderDto>>(command);
            return result.ToHttpResult();
        })
        .RequireAuthorization(Permissions.ManufacturingOrdersUpdate)
        .WithName("StartProductionOrder")
        .WithSummary("Start a production order")
        .WithDescription("Moves a released production order to InProgress.")
        .Produces<ProductionOrderDto>(StatusCodes.Status200OK)
        .Produces<ProblemDetails>(StatusCodes.Status404NotFound)
        .Produces<ProblemDetails>(StatusCodes.Status409Conflict);

        // Report progress
        group.MapPost("/{id:guid}/progress", async (
            Guid id,
            ReportProductionProgressRequest request,
            [FromServices] ICurrentUser currentUser,
            IMessageBus bus) =>
        {
            var command = new ReportProductionProgressCommand(
                id,
                request.ProducedQuantity,
                request.ScrapQuantity)
            {
                UserId = currentUser.UserId
            };
            var result = await bus.InvokeAsync<Result<ProductionOrderDto>>(command);
            return result.ToHttpResult();
        })
        .RequireAuthorization(Permissions.ManufacturingOrdersUpdate)
        .WithName("ReportProductionProgress")
        .WithSummary("Report production progress")
        .WithDescription("Adds produced/scrap quantity increments to an in-progress production order.")
        .Produces<ProductionOrderDto>(StatusCodes.Status200OK)
        .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
        .Produces<ProblemDetails>(StatusCodes.Status404NotFound)
        .Produces<ProblemDetails>(StatusCodes.Status409Conflict);

        // Complete production order
        group.MapPost("/{id:guid}/complete", async (
            Guid id,
            [FromServices] ICurrentUser currentUser,
            IMessageBus bus) =>
        {
            var command = new CompleteProductionOrderCommand(id) { UserId = currentUser.UserId };
            var result = await bus.InvokeAsync<Result<ProductionOrderDto>>(command);
            return result.ToHttpResult();
        })
        .RequireAuthorization(Permissions.ManufacturingOrdersManage)
        .WithName("CompleteProductionOrder")
        .WithSummary("Complete a production order")
        .WithDescription("Moves an in-progress production order to Completed.")
        .Produces<ProductionOrderDto>(StatusCodes.Status200OK)
        .Produces<ProblemDetails>(StatusCodes.Status404NotFound)
        .Produces<ProblemDetails>(StatusCodes.Status409Conflict);

        // Cancel production order
        group.MapPost("/{id:guid}/cancel", async (
            Guid id,
            CancelProductionOrderRequest request,
            [FromServices] ICurrentUser currentUser,
            IMessageBus bus) =>
        {
            var command = new CancelProductionOrderCommand(id, request.Reason) { UserId = currentUser.UserId };
            var result = await bus.InvokeAsync<Result<ProductionOrderDto>>(command);
            return result.ToHttpResult();
        })
        .RequireAuthorization(Permissions.ManufacturingOrdersManage)
        .WithName("CancelProductionOrder")
        .WithSummary("Cancel a production order")
        .WithDescription("Cancels a draft, released, or in-progress production order.")
        .Produces<ProductionOrderDto>(StatusCodes.Status200OK)
        .Produces<ProblemDetails>(StatusCodes.Status404NotFound)
        .Produces<ProblemDetails>(StatusCodes.Status409Conflict);

        // Issue material
        group.MapPost("/{id:guid}/issues", async (
            Guid id,
            IssueMaterialRequest request,
            [FromServices] ICurrentUser currentUser,
            IMessageBus bus) =>
        {
            var command = new IssueMaterialCommand(
                id,
                request.ProductionOrderItemId,
                request.Quantity)
            {
                UserId = currentUser.UserId
            };
            var result = await bus.InvokeAsync<Result<ProductionOrderDto>>(command);
            return result.ToHttpResult();
        })
        .RequireAuthorization(Permissions.ManufacturingOrdersUpdate)
        .WithName("IssueMaterial")
        .WithSummary("Issue material")
        .WithDescription("Issues (consumes) material against a production order requirement line.")
        .Produces<ProductionOrderDto>(StatusCodes.Status200OK)
        .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
        .Produces<ProblemDetails>(StatusCodes.Status404NotFound)
        .Produces<ProblemDetails>(StatusCodes.Status409Conflict);
    }
}
