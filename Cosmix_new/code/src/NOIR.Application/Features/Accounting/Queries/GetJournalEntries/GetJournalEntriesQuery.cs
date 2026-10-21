namespace NOIR.Application.Features.Accounting.Queries.GetJournalEntries;

/// <summary>
/// Query to get paged list of journal entries.
/// </summary>
public sealed record GetJournalEntriesQuery(
    JournalEntryStatus? Status = null,
    DateOnly? FromDate = null,
    DateOnly? ToDate = null,
    string? Search = null,
    int Page = 1,
    int PageSize = 20);
