namespace NOIR.Application.Features.Estimation.Commands.UpdateBoqItem;

/// <summary>
/// Wolverine handler for updating a BOQ line item.
/// </summary>
public class UpdateBoqItemCommandHandler
{
    private readonly IRepository<Boq, Guid> _boqRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUser _currentUser;
    private readonly IEntityUpdateHubContext _entityUpdateHub;

    public UpdateBoqItemCommandHandler(
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

    public async Task<Result<BoqItemDto>> Handle(
        UpdateBoqItemCommand command,
        CancellationToken cancellationToken)
    {
        var spec = new BoqByIdForUpdateSpec(command.BoqId);
        var boq = await _boqRepository.FirstOrDefaultAsync(spec, cancellationToken);

        if (boq == null)
        {
            return Result.Failure<BoqItemDto>(
                Error.NotFound($"BOQ with ID '{command.BoqId}' was not found.", "NOIR-EST-001"));
        }

        var item = boq.Items.FirstOrDefault(i => i.Id == command.ItemId);
        if (item == null)
        {
            return Result.Failure<BoqItemDto>(
                Error.NotFound($"BOQ item with ID '{command.ItemId}' was not found.", "NOIR-EST-004"));
        }

        item.Update(
            command.Section,
            command.ItemCode,
            command.Description,
            command.Quantity,
            command.Unit,
            command.UnitRate,
            command.SortOrder);

        boq.RecalculateTotals();

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await _entityUpdateHub.PublishEntityUpdatedAsync(
            entityType: "Boq",
            entityId: boq.Id,
            operation: EntityOperation.Updated,
            tenantId: _currentUser.TenantId!,
            cancellationToken);

        return Result.Success(BoqMapper.ToItemDto(item));
    }
}
