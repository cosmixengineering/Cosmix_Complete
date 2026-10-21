using NOIR.Application.Features.Crm.Commands.CreateLead;
using NOIR.Application.Features.Crm.Commands.UpdateLead;
using NOIR.Application.Features.Crm.Commands.MoveLeadStage;
using NOIR.Application.Features.Crm.Commands.WinLead;
using NOIR.Application.Features.Crm.Commands.LoseLead;
using NOIR.Application.Features.Crm.Commands.ReopenLead;
using NOIR.Application.Features.Crm.Commands.ReorderLead;
using NOIR.Application.Features.Crm.Queries.GetLeads;
using NOIR.Application.Features.Crm.Queries.GetLeadById;
using NOIR.Application.Features.Crm.DTOs;

namespace NOIR.Web.Endpoints;

public static class LeadEndpoints
{
    public static void MapLeadEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/crm/leads")
            .WithTags("CRM - Leads")
            .RequireFeature(ModuleNames.Erp.Crm)
            .RequireAuthorization();

        group.MapGet("/", async (
            [FromQuery] Guid? pipelineId,
            [FromQuery] Guid? stageId,
            [FromQuery] Guid? ownerId,
            [FromQuery] LeadStatus? status,
            [FromQuery] int? page,
            [FromQuery] int? pageSize,
            IMessageBus bus) =>
        {
            var query = new GetLeadsQuery(pipelineId, stageId, ownerId, status, page ?? 1, pageSize ?? 20);
            var result = await bus.InvokeAsync<Result<PagedResult<LeadDto>>>(query);
            return result.ToHttpResult();
        })
        .RequireAuthorization(Permissions.CrmLeadsRead)
        .WithName("GetLeads")
        .WithSummary("Get paginated list of leads")
        .Produces<PagedResult<LeadDto>>(StatusCodes.Status200OK);

        group.MapGet("/{id:guid}", async (Guid id, IMessageBus bus) =>
        {
            var query = new GetLeadByIdQuery(id);
            var result = await bus.InvokeAsync<Result<LeadDto>>(query);
            return result.ToHttpResult();
        })
        .RequireAuthorization(Permissions.CrmLeadsRead)
        .WithName("GetLeadById")
        .WithSummary("Get lead by ID")
        .Produces<LeadDto>(StatusCodes.Status200OK)
        .Produces<ProblemDetails>(StatusCodes.Status404NotFound);

        group.MapPost("/", async (
            CreateLeadRequest request,
            [FromServices] ICurrentUser currentUser,
            IMessageBus bus) =>
        {
            var command = new CreateLeadCommand(
                request.Title, request.ContactId, request.PipelineId, request.CompanyId,
                request.Value, request.Currency, request.OwnerId,
                request.ExpectedCloseDate, request.Notes)
            {
                AuditUserId = currentUser.UserId
            };
            var result = await bus.InvokeAsync<Result<LeadDto>>(command);
            return result.ToHttpResult();
        })
        .RequireAuthorization(Permissions.CrmLeadsCreate)
        .WithName("CreateLead")
        .WithSummary("Create a new lead")
        .Produces<LeadDto>(StatusCodes.Status200OK)
        .Produces<ProblemDetails>(StatusCodes.Status400BadRequest);

        group.MapPut("/{id:guid}", async (
            Guid id,
            UpdateLeadRequest request,
            [FromServices] ICurrentUser currentUser,
            IMessageBus bus) =>
        {
            var command = new UpdateLeadCommand(
                id, request.Title, request.ContactId, request.CompanyId,
                request.Value, request.Currency, request.OwnerId,
                request.ExpectedCloseDate, request.Notes)
            {
                AuditUserId = currentUser.UserId
            };
            var result = await bus.InvokeAsync<Result<LeadDto>>(command);
            return result.ToHttpResult();
        })
        .RequireAuthorization(Permissions.CrmLeadsUpdate)
        .WithName("UpdateLead")
        .WithSummary("Update a lead")
        .Produces<LeadDto>(StatusCodes.Status200OK)
        .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
        .Produces<ProblemDetails>(StatusCodes.Status404NotFound);

        group.MapPost("/{id:guid}/move-stage", async (
            Guid id,
            MoveLeadStageRequest request,
            [FromServices] ICurrentUser currentUser,
            IMessageBus bus) =>
        {
            var command = new MoveLeadStageCommand(id, request.NewStageId, request.NewSortOrder)
            {
                AuditUserId = currentUser.UserId
            };
            var result = await bus.InvokeAsync<Result<LeadDto>>(command);
            return result.ToHttpResult();
        })
        .RequireAuthorization(Permissions.CrmLeadsUpdate)
        .WithName("MoveLeadStage")
        .WithSummary("Move a lead to a different pipeline stage")
        .Produces<LeadDto>(StatusCodes.Status200OK)
        .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
        .Produces<ProblemDetails>(StatusCodes.Status404NotFound);

        group.MapPost("/{id:guid}/win", async (
            Guid id,
            [FromServices] ICurrentUser currentUser,
            IMessageBus bus) =>
        {
            var command = new WinLeadCommand(id) { AuditUserId = currentUser.UserId };
            var result = await bus.InvokeAsync<Result<LeadDto>>(command);
            return result.ToHttpResult();
        })
        .RequireAuthorization(Permissions.CrmLeadsManage)
        .WithName("WinLead")
        .WithSummary("Mark a lead as won")
        .Produces<LeadDto>(StatusCodes.Status200OK)
        .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
        .Produces<ProblemDetails>(StatusCodes.Status404NotFound);

        group.MapPost("/{id:guid}/lose", async (
            Guid id,
            LoseLeadRequest request,
            [FromServices] ICurrentUser currentUser,
            IMessageBus bus) =>
        {
            var command = new LoseLeadCommand(id, request.Reason) { AuditUserId = currentUser.UserId };
            var result = await bus.InvokeAsync<Result<LeadDto>>(command);
            return result.ToHttpResult();
        })
        .RequireAuthorization(Permissions.CrmLeadsManage)
        .WithName("LoseLead")
        .WithSummary("Mark a lead as lost")
        .Produces<LeadDto>(StatusCodes.Status200OK)
        .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
        .Produces<ProblemDetails>(StatusCodes.Status404NotFound);

        group.MapPost("/{id:guid}/reopen", async (
            Guid id,
            [FromServices] ICurrentUser currentUser,
            IMessageBus bus) =>
        {
            var command = new ReopenLeadCommand(id) { AuditUserId = currentUser.UserId };
            var result = await bus.InvokeAsync<Result<LeadDto>>(command);
            return result.ToHttpResult();
        })
        .RequireAuthorization(Permissions.CrmLeadsManage)
        .WithName("ReopenLead")
        .WithSummary("Reopen a won or lost lead")
        .Produces<LeadDto>(StatusCodes.Status200OK)
        .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
        .Produces<ProblemDetails>(StatusCodes.Status404NotFound);

        group.MapPut("/{id:guid}/reorder", async (
            Guid id,
            [FromBody] double newSortOrder,
            [FromServices] ICurrentUser currentUser,
            IMessageBus bus) =>
        {
            var command = new ReorderLeadCommand(id, newSortOrder) { AuditUserId = currentUser.UserId };
            var result = await bus.InvokeAsync<Result<LeadDto>>(command);
            return result.ToHttpResult();
        })
        .RequireAuthorization(Permissions.CrmLeadsUpdate)
        .WithName("ReorderLead")
        .WithSummary("Update lead sort order within a stage")
        .Produces<LeadDto>(StatusCodes.Status200OK)
        .Produces<ProblemDetails>(StatusCodes.Status404NotFound);
    }
}
