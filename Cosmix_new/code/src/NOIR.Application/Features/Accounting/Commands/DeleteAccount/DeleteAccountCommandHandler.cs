namespace NOIR.Application.Features.Accounting.Commands.DeleteAccount;

/// <summary>
/// Wolverine handler for deleting an account (soft delete).
/// </summary>
public class DeleteAccountCommandHandler
{
    private readonly IRepository<Account, Guid> _accountRepository;
    private readonly IRepository<JournalEntry, Guid> _journalEntryRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUser _currentUser;
    private readonly IEntityUpdateHubContext _entityUpdateHub;

    public DeleteAccountCommandHandler(
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

    public async Task<Result<bool>> Handle(
        DeleteAccountCommand command,
        CancellationToken cancellationToken)
    {
        // Get the account for update (need tracking for soft delete)
        var spec = new AccountByIdForUpdateSpec(command.Id);
        var account = await _accountRepository.FirstOrDefaultAsync(spec, cancellationToken);

        if (account == null)
        {
            return Result.Failure<bool>(
                Error.NotFound($"Account with ID '{command.Id}' was not found.", ErrorCodes.Business.NotFound));
        }

        // System accounts are part of the base chart of accounts and cannot be removed
        if (account.IsSystem)
        {
            return Result.Failure<bool>(
                Error.Conflict("System accounts cannot be deleted.", ErrorCodes.Business.CannotDelete));
        }

        // Block deletion when child accounts still reference this account
        var hasChildren = await _accountRepository.AnyAsync(
            a => a.ParentId == account.Id, cancellationToken);

        if (hasChildren)
        {
            return Result.Failure<bool>(
                Error.Conflict("Cannot delete an account that has child accounts. Reassign or delete the children first.", ErrorCodes.Business.CannotDelete));
        }

        // Block deletion when journal lines reference this account (preserve ledger integrity)
        var hasJournalLinesSpec = new JournalEntriesUsingAccountSpec(account.Id);
        var hasJournalLines = await _journalEntryRepository.AnyAsync(hasJournalLinesSpec, cancellationToken);

        if (hasJournalLines)
        {
            return Result.Failure<bool>(
                Error.Conflict("Cannot delete an account that is referenced by journal entries.", ErrorCodes.Business.CannotDelete));
        }

        // Soft delete the account (handled by interceptor)
        _accountRepository.Remove(account);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await _entityUpdateHub.PublishEntityUpdatedAsync(
            entityType: "Account",
            entityId: account.Id,
            operation: EntityOperation.Deleted,
            tenantId: _currentUser.TenantId!,
            cancellationToken);

        return Result.Success(true);
    }
}
