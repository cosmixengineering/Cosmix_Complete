namespace NOIR.Application.Features.Accounting.Commands.VoidJournalEntry;

/// <summary>
/// Wolverine handler for voiding a posted journal entry.
/// </summary>
public class VoidJournalEntryCommandHandler
{
    private readonly IRepository<JournalEntry, Guid> _journalEntryRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUser _currentUser;
    private readonly IEntityUpdateHubContext _entityUpdateHub;

    public VoidJournalEntryCommandHandler(
        IRepository<JournalEntry, Guid> journalEntryRepository,
        IUnitOfWork unitOfWork,
        ICurrentUser currentUser,
        IEntityUpdateHubContext entityUpdateHub)
    {
        _journalEntryRepository = journalEntryRepository;
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
        _entityUpdateHub = entityUpdateHub;
    }

    public async Task<Result<JournalEntryDto>> Handle(
        VoidJournalEntryCommand command,
        CancellationToken cancellationToken)
    {
        // Get the entry for update
        var spec = new JournalEntryByIdForUpdateSpec(command.Id);
        var entry = await _journalEntryRepository.FirstOrDefaultAsync(spec, cancellationToken);

        if (entry == null)
        {
            return Result.Failure<JournalEntryDto>(
                Error.NotFound($"Journal entry with ID '{command.Id}' was not found.", ErrorCodes.Business.NotFound));
        }

        try
        {
            entry.Void(command.UserId, command.Reason);
        }
        catch (InvalidOperationException ex)
        {
            return Result.Failure<JournalEntryDto>(
                Error.Validation("Status", ex.Message));
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
