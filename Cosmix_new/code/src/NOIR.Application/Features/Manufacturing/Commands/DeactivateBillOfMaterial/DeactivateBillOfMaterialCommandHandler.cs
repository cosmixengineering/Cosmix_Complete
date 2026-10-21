namespace NOIR.Application.Features.Manufacturing.Commands.DeactivateBillOfMaterial;

/// <summary>
/// Wolverine handler for deactivating a bill of material.
/// </summary>
public class DeactivateBillOfMaterialCommandHandler
{
    private readonly IRepository<BillOfMaterial, Guid> _bomRepository;
    private readonly IRepository<Product, Guid> _productRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUser _currentUser;
    private readonly IEntityUpdateHubContext _entityUpdateHub;

    public DeactivateBillOfMaterialCommandHandler(
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
        DeactivateBillOfMaterialCommand command,
        CancellationToken cancellationToken)
    {
        var spec = new BillOfMaterialByIdForUpdateSpec(command.Id);
        var bom = await _bomRepository.FirstOrDefaultAsync(spec, cancellationToken);

        if (bom is null)
        {
            return Result.Failure<BillOfMaterialDto>(
                Error.NotFound($"Bill of material with ID '{command.Id}' not found."));
        }

        bom.Deactivate();
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
