namespace NOIR.Application.Features.Accounting.Queries.GetJournalEntryById;

/// <summary>
/// Wolverine handler for getting a journal entry by ID with its lines.
/// Account codes/names are resolved in a single extra query.
/// </summary>
public class GetJournalEntryByIdQueryHandler
{
    private readonly IRepository<JournalEntry, Guid> _journalEntryRepository;
    private readonly IRepository<Account, Guid> _accountRepository;

    public GetJournalEntryByIdQueryHandler(
        IRepository<JournalEntry, Guid> journalEntryRepository,
        IRepository<Account, Guid> accountRepository)
    {
        _journalEntryRepository = journalEntryRepository;
        _accountRepository = accountRepository;
    }

    public async Task<Result<JournalEntryDto>> Handle(
        GetJournalEntryByIdQuery query,
        CancellationToken cancellationToken)
    {
        var spec = new JournalEntryByIdSpec(query.Id);
        var entry = await _journalEntryRepository.FirstOrDefaultAsync(spec, cancellationToken);

        if (entry == null)
        {
            return Result.Failure<JournalEntryDto>(
                Error.NotFound($"Journal entry with ID '{query.Id}' was not found.", ErrorCodes.Business.NotFound));
        }

        // Resolve account snapshot for each line
        var accountIds = entry.Lines
            .Select(l => l.AccountId)
            .Distinct()
            .ToList();

        var accountsById = new Dictionary<Guid, Account>();

        if (accountIds.Count > 0)
        {
            var accountsSpec = new AccountsByIdsSpec(accountIds);
            var accounts = await _accountRepository.ListAsync(accountsSpec, cancellationToken);
            accountsById = accounts.ToDictionary(a => a.Id);
        }

        var lineDtos = entry.Lines
            .Select(l => AccountingMapper.ToLineDto(
                l,
                accountsById.TryGetValue(l.AccountId, out var account) ? account.Code : null,
                accountsById.TryGetValue(l.AccountId, out var accountForName) ? accountForName.Name : null))
            .ToList();

        return Result.Success(AccountingMapper.ToEntryDto(entry, lineDtos));
    }
}
