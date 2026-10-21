using NOIR.Application.Features.Accounting.Commands.CreateAccount;
using NOIR.Application.Features.Accounting.Commands.DeleteAccount;
using NOIR.Application.Features.Accounting.Commands.UpdateAccount;
using NOIR.Application.Features.Accounting.DTOs;
using NOIR.Application.Features.Accounting.Queries.GetAccountById;
using NOIR.Application.Features.Accounting.Queries.GetAccounts;
using NOIR.Domain.Entities.Accounting;

namespace NOIR.Web.Endpoints;

/// <summary>
/// Accounting account API endpoints.
/// Provides CRUD operations for the chart of accounts.
/// </summary>
public static class AccountEndpoints
{
    public static void MapAccountEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/accounting/accounts")
            .WithTags("Accounting - Accounts")
            .RequireFeature(ModuleNames.Erp.Accounting)
            .RequireAuthorization();

        // Get all accounts (paged)
        group.MapGet("/", async (
            [FromQuery] string? search,
            [FromQuery] AccountType? type,
            [FromQuery] bool? isActive,
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 100,
            IMessageBus bus = null!) =>
        {
            var query = new GetAccountsQuery(search, type, isActive, pageNumber, pageSize);
            var result = await bus.InvokeAsync<Result<PagedResult<AccountListDto>>>(query);
            return result.ToHttpResult();
        })
        .RequireAuthorization(Permissions.AccountingAccountsRead)
        .WithName("GetAccounts")
        .WithSummary("Get list of accounts")
        .WithDescription("Returns paged chart of accounts with optional filtering.")
        .Produces<PagedResult<AccountListDto>>(StatusCodes.Status200OK);

        // Get account by ID
        group.MapGet("/{id:guid}", async (Guid id, IMessageBus bus) =>
        {
            var query = new GetAccountByIdQuery(id);
            var result = await bus.InvokeAsync<Result<AccountDto>>(query);
            return result.ToHttpResult();
        })
        .RequireAuthorization(Permissions.AccountingAccountsRead)
        .WithName("GetAccountById")
        .WithSummary("Get account by ID")
        .WithDescription("Returns account details by ID.")
        .Produces<AccountDto>(StatusCodes.Status200OK)
        .Produces<ProblemDetails>(StatusCodes.Status404NotFound);

        // Create account
        group.MapPost("/", async (
            CreateAccountRequest request,
            [FromServices] ICurrentUser currentUser,
            IMessageBus bus) =>
        {
            var command = new CreateAccountCommand(
                request.Code,
                request.Name,
                request.Type,
                request.ParentId,
                request.Description)
            {
                UserId = currentUser.UserId
            };
            var result = await bus.InvokeAsync<Result<AccountDto>>(command);
            return result.ToHttpResult();
        })
        .RequireAuthorization(Permissions.AccountingAccountsManage)
        .WithName("CreateAccount")
        .WithSummary("Create a new account")
        .WithDescription("Creates a new chart of accounts entry.")
        .Produces<AccountDto>(StatusCodes.Status200OK)
        .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
        .Produces<ProblemDetails>(StatusCodes.Status409Conflict);

        // Update account
        group.MapPut("/{id:guid}", async (
            Guid id,
            UpdateAccountRequest request,
            [FromServices] ICurrentUser currentUser,
            IMessageBus bus) =>
        {
            var command = new UpdateAccountCommand(
                id,
                request.Code,
                request.Name,
                request.ParentId,
                request.Description)
            {
                UserId = currentUser.UserId
            };
            var result = await bus.InvokeAsync<Result<AccountDto>>(command);
            return result.ToHttpResult();
        })
        .RequireAuthorization(Permissions.AccountingAccountsManage)
        .WithName("UpdateAccount")
        .WithSummary("Update an account")
        .WithDescription("Updates account details.")
        .Produces<AccountDto>(StatusCodes.Status200OK)
        .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
        .Produces<ProblemDetails>(StatusCodes.Status404NotFound)
        .Produces<ProblemDetails>(StatusCodes.Status409Conflict);

        // Delete account (soft delete)
        group.MapDelete("/{id:guid}", async (
            Guid id,
            [FromServices] ICurrentUser currentUser,
            IMessageBus bus) =>
        {
            var command = new DeleteAccountCommand(id) { UserId = currentUser.UserId };
            var result = await bus.InvokeAsync<Result<bool>>(command);
            return result.ToHttpResult();
        })
        .RequireAuthorization(Permissions.AccountingAccountsManage)
        .WithName("DeleteAccount")
        .WithSummary("Soft-delete an account")
        .WithDescription("Soft-deletes an account. Will fail if it is a system account, has children, or is referenced by journal entries.")
        .Produces(StatusCodes.Status200OK)
        .Produces<ProblemDetails>(StatusCodes.Status404NotFound)
        .Produces<ProblemDetails>(StatusCodes.Status409Conflict);
    }
}
