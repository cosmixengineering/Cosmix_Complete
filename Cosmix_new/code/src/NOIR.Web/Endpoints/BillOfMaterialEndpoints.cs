using NOIR.Application.Features.Manufacturing.Commands.ActivateBillOfMaterial;
using NOIR.Application.Features.Manufacturing.Commands.AddBillOfMaterialItem;
using NOIR.Application.Features.Manufacturing.Commands.CreateBillOfMaterial;
using NOIR.Application.Features.Manufacturing.Commands.DeactivateBillOfMaterial;
using NOIR.Application.Features.Manufacturing.Commands.DeleteBillOfMaterial;
using NOIR.Application.Features.Manufacturing.Commands.RemoveBillOfMaterialItem;
using NOIR.Application.Features.Manufacturing.Commands.UpdateBillOfMaterial;
using NOIR.Application.Features.Manufacturing.DTOs;
using NOIR.Application.Features.Manufacturing.Queries.GetBillOfMaterialById;
using NOIR.Application.Features.Manufacturing.Queries.GetBillOfMaterials;

namespace NOIR.Web.Endpoints;

/// <summary>
/// Manufacturing bill of material API endpoints.
/// Provides CRUD, component management, and activation for Bills of Material.
/// </summary>
public static class BillOfMaterialEndpoints
{
    public static void MapBillOfMaterialEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/manufacturing/boms")
            .WithTags("Manufacturing - BOMs")
            .RequireFeature(ModuleNames.Erp.ManufacturingSub.Boms)
            .RequireAuthorization();

        // Get all BOMs (paged)
        group.MapGet("/", async (
            [FromQuery] string? search,
            [FromQuery] bool? isActive,
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 20,
            IMessageBus bus = null!) =>
        {
            var query = new GetBillOfMaterialsQuery(search, isActive, pageNumber, pageSize);
            var result = await bus.InvokeAsync<Result<PagedResult<BillOfMaterialListDto>>>(query);
            return result.ToHttpResult();
        })
        .RequireAuthorization(Permissions.ManufacturingBomsRead)
        .WithName("GetBillOfMaterials")
        .WithSummary("Get list of bills of material")
        .WithDescription("Returns paged list of bills of material with optional filtering.")
        .Produces<PagedResult<BillOfMaterialListDto>>(StatusCodes.Status200OK);

        // Get BOM by ID
        group.MapGet("/{id:guid}", async (Guid id, IMessageBus bus) =>
        {
            var query = new GetBillOfMaterialByIdQuery(id);
            var result = await bus.InvokeAsync<Result<BillOfMaterialDto>>(query);
            return result.ToHttpResult();
        })
        .RequireAuthorization(Permissions.ManufacturingBomsRead)
        .WithName("GetBillOfMaterialById")
        .WithSummary("Get bill of material by ID")
        .WithDescription("Returns bill of material details by ID, including component items.")
        .Produces<BillOfMaterialDto>(StatusCodes.Status200OK)
        .Produces<ProblemDetails>(StatusCodes.Status404NotFound);

        // Create BOM
        group.MapPost("/", async (
            CreateBillOfMaterialRequest request,
            [FromServices] ICurrentUser currentUser,
            IMessageBus bus) =>
        {
            var command = new CreateBillOfMaterialCommand(
                request.Code,
                request.Name,
                request.ProductId,
                request.OutputQuantity,
                request.Unit,
                request.Description,
                request.Items)
            {
                UserId = currentUser.UserId
            };
            var result = await bus.InvokeAsync<Result<BillOfMaterialDto>>(command);
            return result.ToHttpResult();
        })
        .RequireAuthorization(Permissions.ManufacturingBomsCreate)
        .WithName("CreateBillOfMaterial")
        .WithSummary("Create a new bill of material")
        .WithDescription("Creates a new bill of material with its component items.")
        .Produces<BillOfMaterialDto>(StatusCodes.Status200OK)
        .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
        .Produces<ProblemDetails>(StatusCodes.Status409Conflict);

        // Update BOM header
        group.MapPut("/{id:guid}", async (
            Guid id,
            UpdateBillOfMaterialRequest request,
            [FromServices] ICurrentUser currentUser,
            IMessageBus bus) =>
        {
            var command = new UpdateBillOfMaterialCommand(
                id,
                request.Name,
                request.ProductId,
                request.OutputQuantity,
                request.Unit,
                request.Description)
            {
                UserId = currentUser.UserId
            };
            var result = await bus.InvokeAsync<Result<BillOfMaterialDto>>(command);
            return result.ToHttpResult();
        })
        .RequireAuthorization(Permissions.ManufacturingBomsUpdate)
        .WithName("UpdateBillOfMaterial")
        .WithSummary("Update a bill of material header")
        .WithDescription("Updates the header fields of a bill of material.")
        .Produces<BillOfMaterialDto>(StatusCodes.Status200OK)
        .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
        .Produces<ProblemDetails>(StatusCodes.Status404NotFound)
        .Produces<ProblemDetails>(StatusCodes.Status409Conflict);

        // Delete BOM (soft delete)
        group.MapDelete("/{id:guid}", async (
            Guid id,
            [FromServices] ICurrentUser currentUser,
            IMessageBus bus) =>
        {
            var command = new DeleteBillOfMaterialCommand(id) { UserId = currentUser.UserId };
            var result = await bus.InvokeAsync<Result<bool>>(command);
            return result.ToHttpResult();
        })
        .RequireAuthorization(Permissions.ManufacturingBomsDelete)
        .WithName("DeleteBillOfMaterial")
        .WithSummary("Soft-delete a bill of material")
        .WithDescription("Soft-deletes a bill of material. Must be inactive and without production orders.")
        .Produces(StatusCodes.Status200OK)
        .Produces<ProblemDetails>(StatusCodes.Status404NotFound)
        .Produces<ProblemDetails>(StatusCodes.Status409Conflict);

        // Add component item
        group.MapPost("/{id:guid}/items", async (
            Guid id,
            AddBillOfMaterialItemRequest request,
            [FromServices] ICurrentUser currentUser,
            IMessageBus bus) =>
        {
            var command = new AddBillOfMaterialItemCommand(
                id,
                request.ProductId,
                request.Quantity,
                request.Unit,
                request.WastePercent,
                request.Notes)
            {
                UserId = currentUser.UserId
            };
            var result = await bus.InvokeAsync<Result<BillOfMaterialDto>>(command);
            return result.ToHttpResult();
        })
        .RequireAuthorization(Permissions.ManufacturingBomsUpdate)
        .WithName("AddBillOfMaterialItem")
        .WithSummary("Add a component item")
        .WithDescription("Adds a component line to an existing bill of material.")
        .Produces<BillOfMaterialDto>(StatusCodes.Status200OK)
        .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
        .Produces<ProblemDetails>(StatusCodes.Status404NotFound)
        .Produces<ProblemDetails>(StatusCodes.Status409Conflict);

        // Remove component item
        group.MapDelete("/{id:guid}/items/{itemId:guid}", async (
            Guid id,
            Guid itemId,
            [FromServices] ICurrentUser currentUser,
            IMessageBus bus) =>
        {
            var command = new RemoveBillOfMaterialItemCommand(id, itemId) { UserId = currentUser.UserId };
            var result = await bus.InvokeAsync<Result<BillOfMaterialDto>>(command);
            return result.ToHttpResult();
        })
        .RequireAuthorization(Permissions.ManufacturingBomsUpdate)
        .WithName("RemoveBillOfMaterialItem")
        .WithSummary("Remove a component item")
        .WithDescription("Removes a component line from a bill of material.")
        .Produces<BillOfMaterialDto>(StatusCodes.Status200OK)
        .Produces<ProblemDetails>(StatusCodes.Status404NotFound)
        .Produces<ProblemDetails>(StatusCodes.Status409Conflict);

        // Activate BOM
        group.MapPost("/{id:guid}/activate", async (
            Guid id,
            [FromServices] ICurrentUser currentUser,
            IMessageBus bus) =>
        {
            var command = new ActivateBillOfMaterialCommand(id) { UserId = currentUser.UserId };
            var result = await bus.InvokeAsync<Result<BillOfMaterialDto>>(command);
            return result.ToHttpResult();
        })
        .RequireAuthorization(Permissions.ManufacturingBomsUpdate)
        .WithName("ActivateBillOfMaterial")
        .WithSummary("Activate a bill of material")
        .WithDescription("Marks a bill of material as active and usable for production orders.")
        .Produces<BillOfMaterialDto>(StatusCodes.Status200OK)
        .Produces<ProblemDetails>(StatusCodes.Status404NotFound);

        // Deactivate BOM
        group.MapPost("/{id:guid}/deactivate", async (
            Guid id,
            [FromServices] ICurrentUser currentUser,
            IMessageBus bus) =>
        {
            var command = new DeactivateBillOfMaterialCommand(id) { UserId = currentUser.UserId };
            var result = await bus.InvokeAsync<Result<BillOfMaterialDto>>(command);
            return result.ToHttpResult();
        })
        .RequireAuthorization(Permissions.ManufacturingBomsUpdate)
        .WithName("DeactivateBillOfMaterial")
        .WithSummary("Deactivate a bill of material")
        .WithDescription("Marks a bill of material as inactive; it cannot be used for new production orders.")
        .Produces<BillOfMaterialDto>(StatusCodes.Status200OK)
        .Produces<ProblemDetails>(StatusCodes.Status404NotFound);
    }
}
