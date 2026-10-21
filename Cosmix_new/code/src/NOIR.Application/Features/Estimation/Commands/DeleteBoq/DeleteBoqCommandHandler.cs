namespace NOIR.Application.Features.Estimation.Commands.DeleteBoq;

/// <summary>
/// Wolverine handler for deleting a BOQ (soft delete).
/// </summary>
public class DeleteBoqCommandHandler
{
    private readonly IRepository<Boq, Guid> _boqRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUser _currentUser;
    private readonly IEntityUpdateHubContext _entityUpdateHub;

    public DeleteBoqCommandHandler(
        IRepository<Boq, Guid> boqRepository,
        IUnitOfWork unitOfWork,
        ICurrentUser currentUser,
        IEntityUpdateHubContext entityUpdateHub)
    {
        _boqRepository = boqRepository;
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
        _entityUpdateHub = entityUpdateHub;
    }

    public async Task<Result<bool>> Handle(
        DeleteBoqCommand command,
        CancellationToken cancellationToken)
    {
        var spec = new BoqByIdForUpdateSpec(command.Id);
        var boq = await _boqRepository.FirstOrDefaultAsync(spec, cancellationToken);

        if (boq == null)
        {
            return Result.Failure<bool>(
                Error.NotFound($"BOQ with ID '{command.Id}' was not found.", "NOIR-EST-001"));
        }

        if (boq.Status != BoqStatus.Draft)
        {
            return Result.Failure<bool>(
                Error.Conflict("Only draft BOQs can be deleted.", "NOIR-EST-003"));
        }

        // Soft delete (handled by AuditableEntityInterceptor)
        _boqRepository.Remove(boq);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await _entityUpdateHub.PublishEntityUpdatedAsync(
            entityType: "Boq",
            entityId: boq.Id,
            operation: EntityOperation.Deleted,
            tenantId: _currentUser.TenantId!,
            cancellationToken);

        return Result.Success(true);
    }
}
