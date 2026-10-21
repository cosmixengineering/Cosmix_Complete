namespace NOIR.Application.Features.Manufacturing.Commands.UpdateBillOfMaterial;

/// <summary>
/// Wolverine handler for updating a bill of material header.
/// </summary>
public class UpdateBillOfMaterialCommandHandler
{
    private readonly IRepository<BillOfMaterial, Guid> _bomRepository;
    private readonly IRepository<Product, Guid> _productRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUser _currentUser;
    private readonly IEntityUpdateHubContext _entityUpdateHub;

    public UpdateBillOfMaterialCommandHandler(
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
        UpdateBillOfMaterialCommand command,
        CancellationToken cancellationToken)
    {
        var spec = new BillOfMaterialByIdForUpdateSpec(command.Id);
        var bom = await _bomRepository.FirstOrDefaultAsync(spec, cancellationToken);

        if (bom is null)
        {
            return Result.Failure<BillOfMaterialDto>(
                Error.NotFound($"Bill of material with ID '{command.Id}' not found."));
        }

        // The BOM code is immutable on update - no duplicate check needed.

        var productSpec = new ProductsByIdsSpec([command.ProductId]);
        var product = await _productRepository.FirstOrDefaultAsync(productSpec, cancellationToken);
        if (product is null)
        {
            return Result.Failure<BillOfMaterialDto>(
                Error.NotFound($"Product with ID '{command.ProductId}' not found."));
        }

        try
        {
            bom.UpdateHeader(
                command.Name,
                command.ProductId,
                command.OutputQuantity,
                command.Unit,
                command.Description);
        }
        catch (InvalidOperationException ex)
        {
            return Result.Failure<BillOfMaterialDto>(
                Error.Validation("Header", ex.Message));
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
