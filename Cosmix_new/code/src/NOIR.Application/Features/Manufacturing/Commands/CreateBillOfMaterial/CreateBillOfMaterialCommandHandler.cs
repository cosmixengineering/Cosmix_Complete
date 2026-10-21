namespace NOIR.Application.Features.Manufacturing.Commands.CreateBillOfMaterial;

/// <summary>
/// Wolverine handler for creating a new bill of material.
/// </summary>
public class CreateBillOfMaterialCommandHandler
{
    private readonly IRepository<BillOfMaterial, Guid> _bomRepository;
    private readonly IRepository<Product, Guid> _productRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUser _currentUser;
    private readonly IEntityUpdateHubContext _entityUpdateHub;

    public CreateBillOfMaterialCommandHandler(
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
        CreateBillOfMaterialCommand command,
        CancellationToken cancellationToken)
    {
        // Duplicate code check (unique per tenant)
        var codeSpec = new BillOfMaterialCodeExistsSpec(command.Code);
        var existing = await _bomRepository.FirstOrDefaultAsync(codeSpec, cancellationToken);
        if (existing is not null)
        {
            return Result.Failure<BillOfMaterialDto>(
                Error.Conflict(
                    $"A bill of material with code '{command.Code}' already exists.",
                    ErrorCodes.Business.AlreadyExists));
        }

        // Validate component products exist
        var componentProductIds = command.Items.Select(i => i.ProductId).Append(command.ProductId).Distinct().ToList();
        var products = await _productRepository.ListAsync(new ProductsByIdsSpec(componentProductIds), cancellationToken);
        var knownProductIds = products.Select(p => p.Id).ToHashSet();

        if (!knownProductIds.Contains(command.ProductId))
        {
            return Result.Failure<BillOfMaterialDto>(
                Error.NotFound($"Product with ID '{command.ProductId}' not found."));
        }

        foreach (var missing in command.Items.Where(i => !knownProductIds.Contains(i.ProductId)))
        {
            return Result.Failure<BillOfMaterialDto>(
                Error.NotFound($"Component product with ID '{missing.ProductId}' not found."));
        }

        var bom = BillOfMaterial.Create(
            command.Code,
            command.Name,
            command.ProductId,
            command.OutputQuantity,
            command.Unit,
            command.Description,
            _currentUser.TenantId);

        foreach (var item in command.Items)
        {
            bom.AddItem(BillOfMaterialItem.Create(
                bom.Id,
                item.ProductId,
                item.Quantity,
                item.Unit,
                item.WastePercent,
                item.Notes));
        }

        await _bomRepository.AddAsync(bom, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await _entityUpdateHub.PublishEntityUpdatedAsync(
            entityType: "BillOfMaterial",
            entityId: bom.Id,
            operation: EntityOperation.Created,
            tenantId: _currentUser.TenantId!,
            ct: cancellationToken);

        var productNames = products.ToDictionary(p => p.Id, p => p.Name);

        return Result.Success(ManufacturingMapper.ToDto(bom, productNames));
    }
}
