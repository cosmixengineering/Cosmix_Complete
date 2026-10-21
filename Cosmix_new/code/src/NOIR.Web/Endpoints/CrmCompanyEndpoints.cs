using NOIR.Application.Features.Crm.Commands.CreateCompany;
using NOIR.Application.Features.Crm.Commands.UpdateCompany;
using NOIR.Application.Features.Crm.Commands.DeleteCompany;
using NOIR.Application.Features.Crm.Queries.GetCompanies;
using NOIR.Application.Features.Crm.Queries.GetCompanyById;
using NOIR.Application.Features.Crm.DTOs;

namespace NOIR.Web.Endpoints;

public static class CrmCompanyEndpoints
{
    public static void MapCrmCompanyEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/crm/companies")
            .WithTags("CRM - Companies")
            .RequireFeature(ModuleNames.Erp.Crm)
            .RequireAuthorization();

        group.MapGet("/", async (
            [FromQuery] string? search,
            [FromQuery] int? page,
            [FromQuery] int? pageSize,
            [FromQuery] string? orderBy,
            [FromQuery] bool? isDescending,
            IMessageBus bus) =>
        {
            var query = new GetCompaniesQuery(search, page ?? 1, pageSize ?? 20, orderBy, isDescending ?? true);
            var result = await bus.InvokeAsync<Result<PagedResult<CompanyListDto>>>(query);
            return result.ToHttpResult();
        })
        .RequireAuthorization(Permissions.CrmCompaniesRead)
        .WithName("GetCrmCompanies")
        .WithSummary("Get paginated list of CRM companies")
        .Produces<PagedResult<CompanyListDto>>(StatusCodes.Status200OK);

        group.MapGet("/{id:guid}", async (Guid id, IMessageBus bus) =>
        {
            var query = new GetCompanyByIdQuery(id);
            var result = await bus.InvokeAsync<Result<CompanyDto>>(query);
            return result.ToHttpResult();
        })
        .RequireAuthorization(Permissions.CrmCompaniesRead)
        .WithName("GetCrmCompanyById")
        .WithSummary("Get CRM company by ID")
        .Produces<CompanyDto>(StatusCodes.Status200OK)
        .Produces<ProblemDetails>(StatusCodes.Status404NotFound);

        group.MapPost("/", async (
            CreateCompanyRequest request,
            [FromServices] ICurrentUser currentUser,
            IMessageBus bus) =>
        {
            var command = new CreateCompanyCommand(
                request.Name, request.Domain, request.Industry, request.Address,
                request.Phone, request.Website, request.OwnerId, request.TaxId,
                request.EmployeeCount, request.Notes)
            {
                AuditUserId = currentUser.UserId
            };
            var result = await bus.InvokeAsync<Result<CompanyDto>>(command);
            return result.ToHttpResult();
        })
        .RequireAuthorization(Permissions.CrmCompaniesCreate)
        .WithName("CreateCrmCompany")
        .WithSummary("Create a new CRM company")
        .Produces<CompanyDto>(StatusCodes.Status200OK)
        .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
        .Produces<ProblemDetails>(StatusCodes.Status409Conflict);

        group.MapPut("/{id:guid}", async (
            Guid id,
            UpdateCompanyRequest request,
            [FromServices] ICurrentUser currentUser,
            IMessageBus bus) =>
        {
            var command = new UpdateCompanyCommand(
                id, request.Name, request.Domain, request.Industry, request.Address,
                request.Phone, request.Website, request.OwnerId, request.TaxId,
                request.EmployeeCount, request.Notes)
            {
                AuditUserId = currentUser.UserId
            };
            var result = await bus.InvokeAsync<Result<CompanyDto>>(command);
            return result.ToHttpResult();
        })
        .RequireAuthorization(Permissions.CrmCompaniesUpdate)
        .WithName("UpdateCrmCompany")
        .WithSummary("Update a CRM company")
        .Produces<CompanyDto>(StatusCodes.Status200OK)
        .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
        .Produces<ProblemDetails>(StatusCodes.Status404NotFound);

        group.MapDelete("/{id:guid}", async (
            Guid id,
            [FromServices] ICurrentUser currentUser,
            IMessageBus bus) =>
        {
            var command = new DeleteCompanyCommand(id) { AuditUserId = currentUser.UserId };
            var result = await bus.InvokeAsync<Result<CompanyDto>>(command);
            return result.ToHttpResult();
        })
        .RequireAuthorization(Permissions.CrmCompaniesDelete)
        .WithName("DeleteCrmCompany")
        .WithSummary("Delete a CRM company")
        .Produces<CompanyDto>(StatusCodes.Status200OK)
        .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
        .Produces<ProblemDetails>(StatusCodes.Status404NotFound);
    }
}
