namespace NOIR.Application.Features.Manufacturing.Commands.DeleteBillOfMaterial;

/// <summary>
/// Wolverine handler for deleting a bill of material (soft delete).
/// </summary>
public class DeleteBillOfMaterialCommandHandler
{
    private readonly IRepository<BillOfMaterial, Guid> _bomRepository;
    private readonly IRepository<ProductionOrder, Guid> _productionOrderRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUser _currentUser;
    private readonly IEntityUpdateHubContext _entityUpdateHub;

    public DeleteBillOfMaterialCommandHandler(
        IRepository<BillOfMaterial, Guid> bomRepository,
        IRepository<ProductionOrder, Guid> productionOrderRepository,
        IUnitOfWork unitOfWork,
        ICurrentUser currentUser,
        IEntityUpdateHubContext entityUpdateHub)
    {
        _bomRepository = bomRepository;
        _productionOrderRepository = productionOrderRepository;
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
        _entityUpdateHub = entityUpdateHub;
    }

    public async Task<Result<bool>> Handle(
        DeleteBillOfMaterialCommand command,
        CancellationToken cancellationToken)
    {
        var spec = new BillOfMaterialByIdForUpdateSpec(command.Id);
        var bom = await _bomRepository.FirstOrDefaultAsync(spec, cancellationToken);

        if (bom is null)
        {
            return Result.Failure<bool>(
                Error.NotFound($"Bill of material with ID '{command.Id}' not found."));
        }

        if (bom.IsActive)
        {
            return Result.Failure<bool>(
                Error.Conflict(
                    "Deactivate the bill of material before deleting it.",
                    ErrorCodes.Business.CannotDelete));
        }

        var hasOrdersSpec = new ProductionOrdersByBomIdSpec(command.Id);
        var hasProductionOrders = await _productionOrderRepository.AnyAsync(hasOrdersSpec, cancellationToken);

        if (hasProductionOrders)
        {
            return Result.Failure<bool>(
                Error.Conflict(
                    "Cannot delete a bill of material that has production orders.",
                    ErrorCodes.Business.CannotDelete));
        }

        _bomRepository.Remove(bom);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await _entityUpdateHub.PublishEntityUpdatedAsync(
            entityType: "BillOfMaterial",
            entityId: bom.Id,
            operation: EntityOperation.Deleted,
            tenantId: _currentUser.TenantId!,
            ct: cancellationToken);

        return Result.Success(true);
    }
}
