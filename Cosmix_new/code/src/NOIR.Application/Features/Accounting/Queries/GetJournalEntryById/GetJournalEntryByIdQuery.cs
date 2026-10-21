namespace NOIR.Application.Features.Accounting.Queries.GetJournalEntryById;

/// <summary>
/// Query to get a journal entry by ID with its lines.
/// </summary>
public sealed record GetJournalEntryByIdQuery(Guid Id);
