namespace NOIR.Application.Features.Estimation.Commands.DeleteBoqItem;

/// <summary>
/// Wolverine handler for removing a line item from a BOQ.
/// </summary>
public class DeleteBoqItemCommandHandler
{
    private readonly IRepository<Boq, Guid> _boqRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUser _currentUser;
    private readonly IEntityUpdateHubContext _entityUpdateHub;

    public DeleteBoqItemCommandHandler(
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
        DeleteBoqItemCommand command,
        CancellationToken cancellationToken)
    {
        var spec = new BoqByIdForUpdateSpec(command.BoqId);
        var boq = await _boqRepository.FirstOrDefaultAsync(spec, cancellationToken);

        if (boq == null)
        {
            return Result.Failure<bool>(
                Error.NotFound($"BOQ with ID '{command.BoqId}' was not found.", "NOIR-EST-001"));
        }

        var item = boq.Items.FirstOrDefault(i => i.Id == command.ItemId);
        if (item == null)
        {
            return Result.Failure<bool>(
                Error.NotFound($"BOQ item with ID '{command.ItemId}' was not found.", "NOIR-EST-004"));
        }

        boq.RemoveItem(item.Id);
        boq.RecalculateTotals();

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await _entityUpdateHub.PublishEntityUpdatedAsync(
            entityType: "Boq",
            entityId: boq.Id,
            operation: EntityOperation.Updated,
            tenantId: _currentUser.TenantId!,
            cancellationToken);

        return Result.Success(true);
    }
}
