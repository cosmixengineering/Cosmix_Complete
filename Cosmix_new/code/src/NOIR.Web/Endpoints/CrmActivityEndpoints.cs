using NOIR.Application.Features.Crm.Commands.CreateActivity;
using NOIR.Application.Features.Crm.Commands.UpdateActivity;
using NOIR.Application.Features.Crm.Commands.DeleteActivity;
using NOIR.Application.Features.Crm.Queries.GetActivities;
using NOIR.Application.Features.Crm.Queries.GetActivityById;
using NOIR.Application.Features.Crm.DTOs;

namespace NOIR.Web.Endpoints;

public static class CrmActivityEndpoints
{
    public static void MapCrmActivityEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/crm/activities")
            .WithTags("CRM - Activities")
            .RequireFeature(ModuleNames.Erp.Crm)
            .RequireAuthorization();

        group.MapGet("/", async (
            [FromQuery] Guid? contactId,
            [FromQuery] Guid? leadId,
            [FromQuery] int? page,
            [FromQuery] int? pageSize,
            IMessageBus bus) =>
        {
            var query = new GetActivitiesQuery(contactId, leadId, page ?? 1, pageSize ?? 20);
            var result = await bus.InvokeAsync<Result<PagedResult<ActivityDto>>>(query);
            return result.ToHttpResult();
        })
        .RequireAuthorization(Permissions.CrmActivitiesRead)
        .WithName("GetCrmActivities")
        .WithSummary("Get paginated list of CRM activities")
        .Produces<PagedResult<ActivityDto>>(StatusCodes.Status200OK);

        group.MapGet("/{id:guid}", async (Guid id, IMessageBus bus) =>
        {
            var query = new GetActivityByIdQuery(id);
            var result = await bus.InvokeAsync<Result<ActivityDto>>(query);
            return result.ToHttpResult();
        })
        .RequireAuthorization(Permissions.CrmActivitiesRead)
        .WithName("GetCrmActivityById")
        .WithSummary("Get CRM activity by ID")
        .Produces<ActivityDto>(StatusCodes.Status200OK)
        .Produces<ProblemDetails>(StatusCodes.Status404NotFound);

        group.MapPost("/", async (
            CreateActivityRequest request,
            [FromServices] ICurrentUser currentUser,
            IMessageBus bus) =>
        {
            var command = new CreateActivityCommand(
                request.Type, request.Subject, request.PerformedById, request.PerformedAt,
                request.Description, request.ContactId, request.LeadId, request.DurationMinutes)
            {
                AuditUserId = currentUser.UserId
            };
            var result = await bus.InvokeAsync<Result<ActivityDto>>(command);
            return result.ToHttpResult();
        })
        .RequireAuthorization(Permissions.CrmActivitiesCreate)
        .WithName("CreateCrmActivity")
        .WithSummary("Create a new CRM activity")
        .Produces<ActivityDto>(StatusCodes.Status200OK)
        .Produces<ProblemDetails>(StatusCodes.Status400BadRequest);

        group.MapPut("/{id:guid}", async (
            Guid id,
            UpdateActivityRequest request,
            [FromServices] ICurrentUser currentUser,
            IMessageBus bus) =>
        {
            var command = new UpdateActivityCommand(
                id, request.Type, request.Subject, request.PerformedAt,
                request.Description, request.ContactId, request.LeadId, request.DurationMinutes)
            {
                AuditUserId = currentUser.UserId
            };
            var result = await bus.InvokeAsync<Result<ActivityDto>>(command);
            return result.ToHttpResult();
        })
        .RequireAuthorization(Permissions.CrmActivitiesUpdate)
        .WithName("UpdateCrmActivity")
        .WithSummary("Update a CRM activity")
        .Produces<ActivityDto>(StatusCodes.Status200OK)
        .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
        .Produces<ProblemDetails>(StatusCodes.Status404NotFound);

        group.MapDelete("/{id:guid}", async (
            Guid id,
            [FromServices] ICurrentUser currentUser,
            IMessageBus bus) =>
        {
            var command = new DeleteActivityCommand(id) { AuditUserId = currentUser.UserId };
            var result = await bus.InvokeAsync<Result<ActivityDto>>(command);
            return result.ToHttpResult();
        })
        .RequireAuthorization(Permissions.CrmActivitiesDelete)
        .WithName("DeleteCrmActivity")
        .WithSummary("Delete a CRM activity")
        .Produces<ActivityDto>(StatusCodes.Status200OK)
        .Produces<ProblemDetails>(StatusCodes.Status404NotFound);
    }
}
