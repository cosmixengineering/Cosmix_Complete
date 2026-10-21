namespace NOIR.Application.Features.Accounting.Commands.CreateJournalEntry;

/// <summary>
/// Wolverine handler for creating a new draft journal entry.
/// Entry numbers are auto-generated in the format JE-{yyyyMMdd}-{6-char suffix}.
/// </summary>
public class CreateJournalEntryCommandHandler
{
    private const decimal BalanceTolerance = 0.01m;

    private readonly IRepository<Account, Guid> _accountRepository;
    private readonly IRepository<JournalEntry, Guid> _journalEntryRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUser _currentUser;
    private readonly IEntityUpdateHubContext _entityUpdateHub;

    public CreateJournalEntryCommandHandler(
        IRepository<Account, Guid> accountRepository,
        IRepository<JournalEntry, Guid> journalEntryRepository,
        IUnitOfWork unitOfWork,
        ICurrentUser currentUser,
        IEntityUpdateHubContext entityUpdateHub)
    {
        _accountRepository = accountRepository;
        _journalEntryRepository = journalEntryRepository;
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
        _entityUpdateHub = entityUpdateHub;
    }

    public async Task<Result<JournalEntryDto>> Handle(
        CreateJournalEntryCommand command,
        CancellationToken cancellationToken)
    {
        var tenantId = _currentUser.TenantId;

        // Validate balance at creation time (drafts must still balance)
        var totalDebit = command.Lines.Sum(l => l.Debit);
        var totalCredit = command.Lines.Sum(l => l.Credit);

        if (Math.Abs(totalDebit - totalCredit) > BalanceTolerance)
        {
            return Result.Failure<JournalEntryDto>(
                Error.ValidationErrors(
                [
                    $"Total debits ({totalDebit:0.00}) must equal total credits ({totalCredit:0.00})."
                ]));
        }

        // Verify every referenced account exists
        var accountIds = command.Lines
            .Select(l => l.AccountId)
            .Distinct()
            .ToList();

        var accountsSpec = new AccountsByIdsSpec(accountIds);
        var accounts = await _accountRepository.ListAsync(accountsSpec, cancellationToken);
        var existingAccountIds = accounts.Select(a => a.Id).ToHashSet();

        var missingAccountIds = accountIds.Where(id => !existingAccountIds.Contains(id)).ToList();
        if (missingAccountIds.Count > 0)
        {
            return Result.Failure<JournalEntryDto>(
                Error.ValidationErrors(
                [
                    $"The following account IDs were not found: {string.Join(", ", missingAccountIds)}."
                ]));
        }

        // Create the entry with an auto-generated number
        var entryNumber = GenerateEntryNumber();
        var entry = JournalEntry.Create(
            entryNumber,
            command.EntryDate,
            command.Reference,
            command.Description,
            tenantId);

        foreach (var lineRequest in command.Lines)
        {
            entry.AddLine(JournalLine.Create(
                entry.Id,
                lineRequest.AccountId,
                lineRequest.Debit,
                lineRequest.Credit,
                lineRequest.Memo));
        }

        await _journalEntryRepository.AddAsync(entry, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await _entityUpdateHub.PublishEntityUpdatedAsync(
            entityType: "JournalEntry",
            entityId: entry.Id,
            operation: EntityOperation.Created,
            tenantId: _currentUser.TenantId!,
            cancellationToken);

        var accountCodeLookup = accounts.ToDictionary(a => a.Id, a => a);
        var lineDtos = entry.Lines
            .Select(l => AccountingMapper.ToLineDto(
                l,
                accountCodeLookup.TryGetValue(l.AccountId, out var account) ? account.Code : null,
                accountCodeLookup.TryGetValue(l.AccountId, out var accountForName) ? accountForName.Name : null))
            .ToList();

        return Result.Success(AccountingMapper.ToEntryDto(entry, lineDtos));
    }

    private static string GenerateEntryNumber()
    {
        var suffix = Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();
        return $"JE-{DateTime.UtcNow:yyyyMMdd}-{suffix}";
    }
}
