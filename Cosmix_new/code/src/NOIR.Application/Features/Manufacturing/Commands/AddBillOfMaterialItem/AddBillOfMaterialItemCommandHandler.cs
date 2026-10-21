namespace NOIR.Application.Features.Manufacturing.Commands.AddBillOfMaterialItem;

/// <summary>
/// Wolverine handler for adding a component item to a bill of material.
/// </summary>
public class AddBillOfMaterialItemCommandHandler
{
    private readonly IRepository<BillOfMaterial, Guid> _bomRepository;
    private readonly IRepository<Product, Guid> _productRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUser _currentUser;
    private readonly IEntityUpdateHubContext _entityUpdateHub;

    public AddBillOfMaterialItemCommandHandler(
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
        AddBillOfMaterialItemCommand command,
        CancellationToken cancellationToken)
    {
        var spec = new BillOfMaterialByIdForUpdateSpec(command.BillOfMaterialId);
        var bom = await _bomRepository.FirstOrDefaultAsync(spec, cancellationToken);

        if (bom is null)
        {
            return Result.Failure<BillOfMaterialDto>(
                Error.NotFound($"Bill of material with ID '{command.BillOfMaterialId}' not found."));
        }

        var productSpec = new ProductsByIdsSpec([command.ProductId]);
        var product = await _productRepository.FirstOrDefaultAsync(productSpec, cancellationToken);
        if (product is null)
        {
            return Result.Failure<BillOfMaterialDto>(
                Error.NotFound($"Component product with ID '{command.ProductId}' not found."));
        }

        try
        {
            bom.AddItem(BillOfMaterialItem.Create(
                bom.Id,
                command.ProductId,
                command.Quantity,
                command.Unit,
                command.WastePercent,
                command.Notes));
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
