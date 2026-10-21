using NOIR.Application.Features.Accounting.Commands.AddJournalLine;
using NOIR.Application.Features.Accounting.Commands.CreateJournalEntry;
using NOIR.Application.Features.Accounting.Commands.PostJournalEntry;
using NOIR.Application.Features.Accounting.Commands.RemoveJournalLine;
using NOIR.Application.Features.Accounting.Commands.VoidJournalEntry;
using NOIR.Application.Features.Accounting.DTOs;
using NOIR.Application.Features.Accounting.Queries.GetJournalEntries;
using NOIR.Application.Features.Accounting.Queries.GetJournalEntryById;
using NOIR.Domain.Entities.Accounting;

namespace NOIR.Web.Endpoints;

/// <summary>
/// Accounting journal entry API endpoints.
/// Provides double-entry journal operations with post/void workflow.
/// </summary>
public static class JournalEntryEndpoints
{
    public static void MapJournalEntryEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/accounting/journal")
            .WithTags("Accounting - Journal")
            .RequireFeature(ModuleNames.Erp.Accounting)
            .RequireAuthorization();

        // Get all journal entries (paged)
        group.MapGet("/", async (
            [FromQuery] JournalEntryStatus? status,
            [FromQuery] DateOnly? fromDate,
            [FromQuery] DateOnly? toDate,
            [FromQuery] string? search,
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 20,
            IMessageBus bus = null!) =>
        {
            var query = new GetJournalEntriesQuery(status, fromDate, toDate, search, pageNumber, pageSize);
            var result = await bus.InvokeAsync<Result<PagedResult<JournalEntryListDto>>>(query);
            return result.ToHttpResult();
        })
        .RequireAuthorization(Permissions.AccountingJournalRead)
        .WithName("GetJournalEntries")
        .WithSummary("Get list of journal entries")
        .WithDescription("Returns paged journal entries with optional status, date, and text filtering.")
        .Produces<PagedResult<JournalEntryListDto>>(StatusCodes.Status200OK);

        // Get journal entry by ID
        group.MapGet("/{id:guid}", async (Guid id, IMessageBus bus) =>
        {
            var query = new GetJournalEntryByIdQuery(id);
            var result = await bus.InvokeAsync<Result<JournalEntryDto>>(query);
            return result.ToHttpResult();
        })
        .RequireAuthorization(Permissions.AccountingJournalRead)
        .WithName("GetJournalEntryById")
        .WithSummary("Get journal entry by ID")
        .WithDescription("Returns journal entry details with lines by ID.")
        .Produces<JournalEntryDto>(StatusCodes.Status200OK)
        .Produces<ProblemDetails>(StatusCodes.Status404NotFound);

        // Create journal entry
        group.MapPost("/", async (
            CreateJournalEntryRequest request,
            [FromServices] ICurrentUser currentUser,
            IMessageBus bus) =>
        {
            var command = new CreateJournalEntryCommand(
                request.EntryDate,
                request.Reference,
                request.Description,
                request.Lines ?? [])
            {
                UserId = currentUser.UserId
            };
            var result = await bus.InvokeAsync<Result<JournalEntryDto>>(command);
            return result.ToHttpResult();
        })
        .RequireAuthorization(Permissions.AccountingJournalCreate)
        .WithName("CreateJournalEntry")
        .WithSummary("Create a new journal entry")
        .WithDescription("Creates a draft journal entry with balanced debit/credit lines.")
        .Produces<JournalEntryDto>(StatusCodes.Status200OK)
        .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
        .Produces<ProblemDetails>(StatusCodes.Status409Conflict);

        // Add a line to a journal entry
        group.MapPost("/{id:guid}/lines", async (
            Guid id,
            CreateJournalLineRequest request,
            [FromServices] ICurrentUser currentUser,
            IMessageBus bus) =>
        {
            var command = new AddJournalLineCommand(
                id,
                request.AccountId,
                request.Debit,
                request.Credit,
                request.Memo)
            {
                UserId = currentUser.UserId
            };
            var result = await bus.InvokeAsync<Result<JournalEntryDto>>(command);
            return result.ToHttpResult();
        })
        .RequireAuthorization(Permissions.AccountingJournalCreate)
        .WithName("AddJournalLine")
        .WithSummary("Add a line to a journal entry")
        .WithDescription("Adds a debit or credit line to a draft journal entry.")
        .Produces<JournalEntryDto>(StatusCodes.Status200OK)
        .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
        .Produces<ProblemDetails>(StatusCodes.Status404NotFound);

        // Remove a line from a journal entry
        group.MapDelete("/{id:guid}/lines/{lineId:guid}", async (
            Guid id,
            Guid lineId,
            [FromServices] ICurrentUser currentUser,
            IMessageBus bus) =>
        {
            var command = new RemoveJournalLineCommand(id, lineId) { UserId = currentUser.UserId };
            var result = await bus.InvokeAsync<Result<bool>>(command);
            return result.ToHttpResult();
        })
        .RequireAuthorization(Permissions.AccountingJournalCreate)
        .WithName("RemoveJournalLine")
        .WithSummary("Remove a line from a journal entry")
        .WithDescription("Removes a line from a draft journal entry.")
        .Produces(StatusCodes.Status200OK)
        .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
        .Produces<ProblemDetails>(StatusCodes.Status404NotFound);

        // Post journal entry
        group.MapPost("/{id:guid}/post", async (
            Guid id,
            [FromServices] ICurrentUser currentUser,
            IMessageBus bus) =>
        {
            var command = new PostJournalEntryCommand(id) { UserId = currentUser.UserId };
            var result = await bus.InvokeAsync<Result<JournalEntryDto>>(command);
            return result.ToHttpResult();
        })
        .RequireAuthorization(Permissions.AccountingJournalPost)
        .WithName("PostJournalEntry")
        .WithSummary("Post a journal entry")
        .WithDescription("Posts a draft journal entry. Requires at least two balanced lines.")
        .Produces<JournalEntryDto>(StatusCodes.Status200OK)
        .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
        .Produces<ProblemDetails>(StatusCodes.Status404NotFound);

        // Void journal entry
        group.MapPost("/{id:guid}/void", async (
            Guid id,
            [FromQuery] string? reason,
            [FromServices] ICurrentUser currentUser,
            IMessageBus bus) =>
        {
            var command = new VoidJournalEntryCommand(id, reason) { UserId = currentUser.UserId };
            var result = await bus.InvokeAsync<Result<JournalEntryDto>>(command);
            return result.ToHttpResult();
        })
        .RequireAuthorization(Permissions.AccountingJournalPost)
        .WithName("VoidJournalEntry")
        .WithSummary("Void a journal entry")
        .WithDescription("Voids a posted journal entry, recording the reason for audit.")
        .Produces<JournalEntryDto>(StatusCodes.Status200OK)
        .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
        .Produces<ProblemDetails>(StatusCodes.Status404NotFound);
    }
}
