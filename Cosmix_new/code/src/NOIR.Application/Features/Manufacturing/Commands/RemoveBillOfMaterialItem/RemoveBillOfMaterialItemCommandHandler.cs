namespace NOIR.Application.Features.Manufacturing.Commands.RemoveBillOfMaterialItem;

/// <summary>
/// Wolverine handler for removing a component item from a bill of material.
/// </summary>
public class RemoveBillOfMaterialItemCommandHandler
{
    private readonly IRepository<BillOfMaterial, Guid> _bomRepository;
    private readonly IRepository<Product, Guid> _productRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUser _currentUser;
    private readonly IEntityUpdateHubContext _entityUpdateHub;

    public RemoveBillOfMaterialItemCommandHandler(
        IRepository<BillOfMaterial, Guid> bomRepository,
        IRepository<Product, Guid> productRepository,
        IUnitOfWork unitOfWork,
        ICurrentUser currentUser,
        IEntityUpdateHubContext entityUpdateHub)
    {
        _bomRepository = bomRepository;
        _productRepository = productRepository;
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
        _entityUpdateHub = entityUpdateHub;
    }

    public async Task<Result<BillOfMaterialDto>> Handle(
        RemoveBillOfMaterialItemCommand command,
        CancellationToken cancellationToken)
    {
        var spec = new BillOfMaterialByIdForUpdateSpec(command.BillOfMaterialId);
        var bom = await _bomRepository.FirstOrDefaultAsync(spec, cancellationToken);

        if (bom is null)
        {
            return Result.Failure<BillOfMaterialDto>(
                Error.NotFound($"Bill of material with ID '{command.BillOfMaterialId}' not found."));
        }

        if (!bom.Items.Any(i => i.Id == command.ItemId))
        {
            return Result.Failure<BillOfMaterialDto>(
                Error.NotFound($"BOM item with ID '{command.ItemId}' not found."));
        }

        try
        {
            bom.RemoveItem(command.ItemId);
        }
        catch (InvalidOperationException ex)
        {
            return Result.Failure<BillOfMaterialDto>(
                Error.Conflict(ex.Message, ErrorCodes.Business.Conflict));
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await _entityUpdateHub.PublishEntityUpdatedAsync(
            entityType: "BillOfMaterial",
            entityId: bom.Id,
            operation: EntityOperation.Updated,
            tenantId: _currentUser.TenantId!,
            ct: cancellationToken);

        var productIds = new List<Guid> { bom.ProductId };
        productIds.AddRange(bom.Items.Select(i => i.ProductId));
        var productNames = await ManufacturingMapper.GetProductNamesAsync(
            _productRepository, productIds, cancellationToken);

        return Result.Success(ManufacturingMapper.ToDto(bom, productNames));
    }
}
