namespace NOIR.Application.Features.Manufacturing.Commands.CreateProductionOrder;

/// <summary>
/// Wolverine handler for creating a production order with a BOM snapshot.
/// </summary>
public class CreateProductionOrderCommandHandler
{
    private readonly IRepository<ProductionOrder, Guid> _orderRepository;
    private readonly IRepository<BillOfMaterial, Guid> _bomRepository;
    private readonly IRepository<Product, Guid> _productRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUser _currentUser;
    private readonly IEntityUpdateHubContext _entityUpdateHub;

    public CreateProductionOrderCommandHandler(
        IRepository<ProductionOrder, Guid> orderRepository,
        IRepository<BillOfMaterial, Guid> bomRepository,
        IRepository<Product, Guid> productRepository,
        IUnitOfWork unitOfWork,
        ICurrentUser currentUser,
        IEntityUpdateHubContext entityUpdateHub)
    {
        _orderRepository = orderRepository;
        _bomRepository = bomRepository;
        _productRepository = productRepository;
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
        _entityUpdateHub = entityUpdateHub;
    }

    public async Task<Result<ProductionOrderDto>> Handle(
        CreateProductionOrderCommand command,
        CancellationToken cancellationToken)
    {
        // Duplicate order number check (unique per tenant)
        var numberSpec = new ProductionOrderNumberExistsSpec(command.OrderNumber);
        var numberExists = await _orderRepository.AnyAsync(numberSpec, cancellationToken);
        if (numberExists)
        {
            return Result.Failure<ProductionOrderDto>(
                Error.Conflict(
                    $"A production order with number '{command.OrderNumber}' already exists.",
                    ErrorCodes.Business.AlreadyExists));
        }

        // Load the source BOM with its component items
        var bomSpec = new BillOfMaterialByIdSpec(command.BillOfMaterialId);
        var bom = await _bomRepository.FirstOrDefaultAsync(bomSpec, cancellationToken);

        if (bom is null)
        {
            return Result.Failure<ProductionOrderDto>(
                Error.NotFound($"Bill of material with ID '{command.BillOfMaterialId}' not found."));
        }

        if (!bom.IsActive)
        {
            return Result.Failure<ProductionOrderDto>(
                Error.Conflict(
                    "Cannot create a production order from an inactive bill of material.",
                    ErrorCodes.Business.InvalidState));
        }

        try
        {
            bom.ValidateComplete();
        }
        catch (InvalidOperationException ex)
        {
            return Result.Failure<ProductionOrderDto>(
                Error.Validation("BillOfMaterial", ex.Message));
        }

        Guid? createdByUserId = Guid.TryParse(_currentUser.UserId, out var parsedUserId)
            ? parsedUserId
            : null;

        var order = ProductionOrder.Create(
            command.OrderNumber,
            command.BillOfMaterialId,
            bom.ProductId,
            command.PlannedQuantity,
            command.PlannedStartDate,
            command.PlannedEndDate,
            command.Notes,
            createdByUserId,
            _currentUser.TenantId);

        // Snapshot the BOM components scaled to the planned quantity
        foreach (var bomItem in bom.Items)
        {
            var required = ManufacturingMapper.RoundQuantity(
                bomItem.EffectiveQuantity * command.PlannedQuantity / bom.OutputQuantity);

            order.AddItem(ProductionOrderItem.Create(
                order.Id,
                bomItem.ProductId,
                required,
                bomItem.Unit,
                null));
        }

        await _orderRepository.AddAsync(order, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await _entityUpdateHub.PublishEntityUpdatedAsync(
            entityType: "ProductionOrder",
            entityId: order.Id,
            operation: EntityOperation.Created,
            tenantId: _currentUser.TenantId!,
            ct: cancellationToken);

        var productIds = new List<Guid> { order.ProductId };
        productIds.AddRange(order.Items.Select(i => i.ProductId));
        var productNames = await ManufacturingMapper.GetProductNamesAsync(
            _productRepository, productIds, cancellationToken);

        return Result.Success(ManufacturingMapper.ToDto(order, productNames, bom.Code));
    }
}
