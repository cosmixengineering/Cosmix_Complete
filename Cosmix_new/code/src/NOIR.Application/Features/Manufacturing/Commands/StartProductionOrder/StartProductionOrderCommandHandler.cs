namespace NOIR.Application.Features.Manufacturing.Commands.StartProductionOrder;

/// <summary>
/// Wolverine handler for starting a production order.
/// </summary>
public class StartProductionOrderCommandHandler
{
    private readonly IRepository<ProductionOrder, Guid> _orderRepository;
    private readonly IRepository<Product, Guid> _productRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUser _currentUser;
    private readonly IEntityUpdateHubContext _entityUpdateHub;

    public StartProductionOrderCommandHandler(
        IRepository<ProductionOrder, Guid> orderRepository,
        IRepository<Product, Guid> productRepository,
        IUnitOfWork unitOfWork,
        ICurrentUser currentUser,
        IEntityUpdateHubContext entityUpdateHub)
    {
        _orderRepository = orderRepository;
        _productRepository = productRepository;
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
        _entityUpdateHub = entityUpdateHub;
    }

    public async Task<Result<ProductionOrderDto>> Handle(
        StartProductionOrderCommand command,
        CancellationToken cancellationToken)
    {
        var spec = new ProductionOrderByIdForUpdateSpec(command.Id);
        var order = await _orderRepository.FirstOrDefaultAsync(spec, cancellationToken);

        if (order is null)
        {
            return Result.Failure<ProductionOrderDto>(
                Error.NotFound($"Production order with ID '{command.Id}' not found."));
        }

        try
        {
            order.Start();
        }
        catch (InvalidOperationException ex)
        {
            return Result.Failure<ProductionOrderDto>(
                Error.Conflict(ex.Message, ErrorCodes.Business.InvalidState));
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

        return Result.Success(ManufacturingMapper.ToDto(order, productNames));
    }
}
