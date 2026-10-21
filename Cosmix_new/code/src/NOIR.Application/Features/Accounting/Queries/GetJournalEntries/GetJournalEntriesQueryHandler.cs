namespace NOIR.Application.Features.Accounting.Queries.GetJournalEntries;

/// <summary>
/// Wolverine handler for getting paged list of journal entries.
/// </summary>
public class GetJournalEntriesQueryHandler
{
    private readonly IRepository<JournalEntry, Guid> _journalEntryRepository;

    public GetJournalEntriesQueryHandler(IRepository<JournalEntry, Guid> journalEntryRepository)
    {
        _journalEntryRepository = journalEntryRepository;
    }

    public async Task<Result<PagedResult<JournalEntryListDto>>> Handle(
        GetJournalEntriesQuery query,
        CancellationToken cancellationToken)
    {
        // Get paged entries (spec includes Lines so totals can be computed)
        var spec = new JournalEntriesByFilterSpec(
            query.Status,
            query.FromDate,
            query.ToDate,
            query.Search,
            query.Page,
            query.PageSize);

        var entries = await _journalEntryRepository.ListAsync(spec, cancellationToken);

        // Get total count
        var countSpec = new JournalEntriesCountSpec(
            query.Status,
            query.FromDate,
            query.ToDate,
            query.Search);

        var totalCount = await _journalEntryRepository.CountAsync(countSpec, cancellationToken);

        var items = entries.Select(AccountingMapper.ToEntryListDto).ToList();

        // PagedResult expects pageIndex (0-based), but query.Page is 1-based
        var pageIndex = query.Page - 1;

        return Result.Success(PagedResult<JournalEntryListDto>.Create(
            items,
            totalCount,
            pageIndex,
            query.PageSize));
    }
}
