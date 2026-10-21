namespace NOIR.Application.Features.Accounting.Commands.PostJournalEntry;

/// <summary>
/// Wolverine handler for posting a journal entry.
/// Domain validation errors (unbalanced, too few lines) are converted to validation results.
/// </summary>
public class PostJournalEntryCommandHandler
{
    private readonly IRepository<JournalEntry, Guid> _journalEntryRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUser _currentUser;
    private readonly IEntityUpdateHubContext _entityUpdateHub;

    public PostJournalEntryCommandHandler(
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
        PostJournalEntryCommand command,
        CancellationToken cancellationToken)
    {
        // Get the entry with lines for update
        var spec = new JournalEntryByIdForUpdateSpec(command.Id);
        var entry = await _journalEntryRepository.FirstOrDefaultAsync(spec, cancellationToken);

        if (entry == null)
        {
            return Result.Failure<JournalEntryDto>(
                Error.NotFound($"Journal entry with ID '{command.Id}' was not found.", ErrorCodes.Business.NotFound));
        }

        try
        {
            entry.Post(command.UserId);
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
