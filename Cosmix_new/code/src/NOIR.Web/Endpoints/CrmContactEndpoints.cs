using NOIR.Application.Features.Crm.Commands.CreateContact;
using NOIR.Application.Features.Crm.Commands.UpdateContact;
using NOIR.Application.Features.Crm.Commands.DeleteContact;
using NOIR.Application.Features.Crm.Queries.GetContacts;
using NOIR.Application.Features.Crm.Queries.GetContactById;
using NOIR.Application.Features.Crm.DTOs;

namespace NOIR.Web.Endpoints;

public static class CrmContactEndpoints
{
    public static void MapCrmContactEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/crm/contacts")
            .WithTags("CRM - Contacts")
            .RequireFeature(ModuleNames.Erp.Crm)
            .RequireAuthorization();

        group.MapGet("/", async (
            [FromQuery] string? search,
            [FromQuery] Guid? companyId,
            [FromQuery] Guid? ownerId,
            [FromQuery] ContactSource? source,
            [FromQuery] int? page,
            [FromQuery] int? pageSize,
            [FromQuery] string? orderBy,
            [FromQuery] bool? isDescending,
            IMessageBus bus) =>
        {
            var query = new GetContactsQuery(search, companyId, ownerId, source, page ?? 1, pageSize ?? 20, orderBy, isDescending ?? true);
            var result = await bus.InvokeAsync<Result<PagedResult<ContactListDto>>>(query);
            return result.ToHttpResult();
        })
        .RequireAuthorization(Permissions.CrmContactsRead)
        .WithName("GetCrmContacts")
        .WithSummary("Get paginated list of CRM contacts")
        .Produces<PagedResult<ContactListDto>>(StatusCodes.Status200OK);

        group.MapGet("/{id:guid}", async (Guid id, IMessageBus bus) =>
        {
            var query = new GetContactByIdQuery(id);
            var result = await bus.InvokeAsync<Result<ContactDto>>(query);
            return result.ToHttpResult();
        })
        .RequireAuthorization(Permissions.CrmContactsRead)
        .WithName("GetCrmContactById")
        .WithSummary("Get CRM contact by ID")
        .Produces<ContactDto>(StatusCodes.Status200OK)
        .Produces<ProblemDetails>(StatusCodes.Status404NotFound);

        group.MapPost("/", async (
            CreateContactRequest request,
            [FromServices] ICurrentUser currentUser,
            IMessageBus bus) =>
        {
            var command = new CreateContactCommand(
                request.FirstName, request.LastName, request.Email, request.Source,
                request.Phone, request.JobTitle, request.CompanyId, request.OwnerId, request.Notes)
            {
                AuditUserId = currentUser.UserId
            };
            var result = await bus.InvokeAsync<Result<ContactDto>>(command);
            return result.ToHttpResult();
        })
        .RequireAuthorization(Permissions.CrmContactsCreate)
        .WithName("CreateCrmContact")
        .WithSummary("Create a new CRM contact")
        .Produces<ContactDto>(StatusCodes.Status200OK)
        .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
        .Produces<ProblemDetails>(StatusCodes.Status409Conflict);

        group.MapPut("/{id:guid}", async (
            Guid id,
            UpdateContactRequest request,
            [FromServices] ICurrentUser currentUser,
            IMessageBus bus) =>
        {
            var command = new UpdateContactCommand(
                id, request.FirstName, request.LastName, request.Email, request.Source,
                request.Phone, request.JobTitle, request.CompanyId, request.OwnerId,
                request.CustomerId, request.Notes)
            {
                AuditUserId = currentUser.UserId
            };
            var result = await bus.InvokeAsync<Result<ContactDto>>(command);
            return result.ToHttpResult();
        })
        .RequireAuthorization(Permissions.CrmContactsUpdate)
        .WithName("UpdateCrmContact")
        .WithSummary("Update a CRM contact")
        .Produces<ContactDto>(StatusCodes.Status200OK)
        .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
        .Produces<ProblemDetails>(StatusCodes.Status404NotFound);

        group.MapDelete("/{id:guid}", async (
            Guid id,
            [FromServices] ICurrentUser currentUser,
            IMessageBus bus) =>
        {
            var command = new DeleteContactCommand(id) { AuditUserId = currentUser.UserId };
            var result = await bus.InvokeAsync<Result<ContactDto>>(command);
            return result.ToHttpResult();
        })
        .RequireAuthorization(Permissions.CrmContactsDelete)
        .WithName("DeleteCrmContact")
        .WithSummary("Delete a CRM contact")
        .Produces<ContactDto>(StatusCodes.Status200OK)
        .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
        .Produces<ProblemDetails>(StatusCodes.Status404NotFound);
    }
}
