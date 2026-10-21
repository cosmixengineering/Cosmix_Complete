namespace NOIR.Application.Features.Manufacturing.Commands.UpdateProductionOrder;

/// <summary>
/// Wolverine handler for updating a draft production order header.
/// </summary>
public class UpdateProductionOrderCommandHandler
{
    private readonly IRepository<ProductionOrder, Guid> _orderRepository;
    private readonly IRepository<BillOfMaterial, Guid> _bomRepository;
    private readonly IRepository<Product, Guid> _productRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUser _currentUser;
    private readonly IEntityUpdateHubContext _entityUpdateHub;

    public UpdateProductionOrderCommandHandler(
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
        UpdateProductionOrderCommand command,
        CancellationToken cancellationToken)
    {
        var spec = new ProductionOrderByIdForUpdateSpec(command.Id);
        var order = await _orderRepository.FirstOrDefaultAsync(spec, cancellationToken);

        if (order is null)
        {
            return Result.Failure<ProductionOrderDto>(
                Error.NotFound($"Production order with ID '{command.Id}' not found."));
        }

        var quantityChanged = order.PlannedQuantity != command.PlannedQuantity;

        try
        {
            order.UpdateHeader(
                command.PlannedQuantity,
                command.PlannedStartDate,
                command.PlannedEndDate,
                command.Notes);
        }
        catch (InvalidOperationException ex)
        {
            return Result.Failure<ProductionOrderDto>(
                Error.Conflict(ex.Message, ErrorCodes.Business.InvalidState));
        }

        string? bomCode = null;

        if (quantityChanged)
        {
            // Re-snapshot the material requirements from the BOM at the new scale
            var bomSpec = new BillOfMaterialByIdSpec(order.BillOfMaterialId);
            var bom = await _bomRepository.FirstOrDefaultAsync(bomSpec, cancellationToken);

            if (bom is null)
            {
                return Result.Failure<ProductionOrderDto>(
                    Error.NotFound($"Bill of material with ID '{order.BillOfMaterialId}' not found."));
            }

            bomCode = bom.Code;

            var existingByProduct = order.Items.ToDictionary(i => i.ProductId);

            foreach (var bomItem in bom.Items)
            {
                var required = ManufacturingMapper.RoundQuantity(
                    bomItem.EffectiveQuantity * command.PlannedQuantity / bom.OutputQuantity);

                if (existingByProduct.TryGetValue(bomItem.ProductId, out var existingItem))
                {
                    existingItem.Update(required, bomItem.Unit, existingItem.Notes);
                }
                else
                {
                    order.AddItem(ProductionOrderItem.Create(
                        order.Id,
                        bomItem.ProductId,
                        required,
                        bomItem.Unit,
                        null));
                }
            }

            // Remove snapshot lines no longer present in the BOM (unissued only)
            var bomProductIds = bom.Items.Select(i => i.ProductId).ToHashSet();
            foreach (var stale in order.Items
                         .Where(i => !bomProductIds.Contains(i.ProductId) && i.IssuedQuantity == 0)
                         .ToList())
            {
                order.RemoveItem(stale.Id);
            }
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await _entityUpdateHub.PublishEntityUpdatedAsync(
            entityType: "ProductionOrder",
            entityId: order.Id,
            operation: EntityOperation.Updated,
            tenantId: _currentUser.TenantId!,
            ct: cancellationToken);

        var productIds = new List<Guid> { order.ProductId };
        productIds.AddRange(order.Items.Select(i => i.ProductId));
        var productNames = await ManufacturingMapper.GetProductNamesAsync(
            _productRepository, productIds, cancellationToken);

        return Result.Success(ManufacturingMapper.ToDto(order, productNames, bomCode));
    }
}
