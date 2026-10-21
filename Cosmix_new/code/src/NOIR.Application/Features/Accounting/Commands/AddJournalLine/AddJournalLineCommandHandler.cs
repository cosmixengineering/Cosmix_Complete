namespace NOIR.Application.Features.Accounting.Commands.AddJournalLine;

/// <summary>
/// Wolverine handler for adding a line to a draft journal entry.
/// </summary>
public class AddJournalLineCommandHandler
{
    private readonly IRepository<JournalEntry, Guid> _journalEntryRepository;
    private readonly IRepository<Account, Guid> _accountRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUser _currentUser;
    private readonly IEntityUpdateHubContext _entityUpdateHub;

    public AddJournalLineCommandHandler(
        IRepository<JournalEntry, Guid> journalEntryRepository,
        IRepository<Account, Guid> accountRepository,
        IUnitOfWork unitOfWork,
        ICurrentUser currentUser,
        IEntityUpdateHubContext entityUpdateHub)
    {
        _journalEntryRepository = journalEntryRepository;
        _accountRepository = accountRepository;
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
        _entityUpdateHub = entityUpdateHub;
    }

    public async Task<Result<JournalEntryDto>> Handle(
        AddJournalLineCommand command,
        CancellationToken cancellationToken)
    {
        // Get the entry with lines for update
        var spec = new JournalEntryByIdForUpdateSpec(command.JournalEntryId);
        var entry = await _journalEntryRepository.FirstOrDefaultAsync(spec, cancellationToken);

        if (entry == null)
        {
            return Result.Failure<JournalEntryDto>(
                Error.NotFound($"Journal entry with ID '{command.JournalEntryId}' was not found.", ErrorCodes.Business.NotFound));
        }

        if (entry.Status != JournalEntryStatus.Draft)
        {
            return Result.Failure<JournalEntryDto>(
                Error.Validation("Status", "Only draft journal entries can be modified."));
        }

        // Validate the target account exists
        var accountSpec = new AccountByIdSpec(command.AccountId);
        var account = await _accountRepository.FirstOrDefaultAsync(accountSpec, cancellationToken);

        if (account == null)
        {
            return Result.Failure<JournalEntryDto>(
                Error.Validation("AccountId", $"Account with ID '{command.AccountId}' was not found."));
        }

        // Add the line (factory validates amounts)
        try
        {
            var line = JournalLine.Create(
                entry.Id,
                command.AccountId,
                command.Debit,
                command.Credit,
                command.Memo);

            entry.AddLine(line);
        }
        catch (ArgumentException ex)
        {
            return Result.Failure<JournalEntryDto>(
                Error.Validation("Lines", ex.Message));
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await _entityUpdateHub.PublishEntityUpdatedAsync(
            entityType: "JournalEntry",
            entityId: entry.Id,
            operation: EntityOperation.Updated,
            tenantId: _currentUser.TenantId!,
            cancellationToken);

        return Result.Success(AccountingMapper.ToEntryDto(entry));
    }
}
