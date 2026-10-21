namespace NOIR.Application.Features.Accounting.Commands.RemoveJournalLine;

/// <summary>
/// Wolverine handler for removing a line from a draft journal entry.
/// </summary>
public class RemoveJournalLineCommandHandler
{
    private readonly IRepository<JournalEntry, Guid> _journalEntryRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUser _currentUser;
    private readonly IEntityUpdateHubContext _entityUpdateHub;

    public RemoveJournalLineCommandHandler(
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

    public async Task<Result<bool>> Handle(
        RemoveJournalLineCommand command,
        CancellationToken cancellationToken)
    {
        // Get the entry with lines for update
        var spec = new JournalEntryByIdForUpdateSpec(command.JournalEntryId);
        var entry = await _journalEntryRepository.FirstOrDefaultAsync(spec, cancellationToken);

        if (entry == null)
        {
            return Result.Failure<bool>(
                Error.NotFound($"Journal entry with ID '{command.JournalEntryId}' was not found.", ErrorCodes.Business.NotFound));
        }

        if (entry.Status != JournalEntryStatus.Draft)
        {
            return Result.Failure<bool>(
                Error.Validation("Status", "Only draft journal entries can be modified."));
        }

        if (!entry.Lines.Any(l => l.Id == command.LineId))
        {
            return Result.Failure<bool>(
                Error.NotFound($"Journal line with ID '{command.LineId}' was not found on this entry.", ErrorCodes.Business.NotFound));
        }

        entry.RemoveLine(command.LineId);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await _entityUpdateHub.PublishEntityUpdatedAsync(
            entityType: "JournalEntry",
            entityId: entry.Id,
            operation: EntityOperation.Updated,
            tenantId: _currentUser.TenantId!,
            cancellationToken);

        return Result.Success(true);
    }
}
