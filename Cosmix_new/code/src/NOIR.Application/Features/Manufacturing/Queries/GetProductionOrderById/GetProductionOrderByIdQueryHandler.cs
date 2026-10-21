namespace NOIR.Application.Features.Manufacturing.Queries.GetProductionOrderById;

/// <summary>
/// Wolverine handler for getting a production order by ID.
/// </summary>
public class GetProductionOrderByIdQueryHandler
{
    private readonly IRepository<ProductionOrder, Guid> _orderRepository;
    private readonly IRepository<BillOfMaterial, Guid> _bomRepository;
    private readonly IRepository<Product, Guid> _productRepository;

    public GetProductionOrderByIdQueryHandler(
        IRepository<ProductionOrder, Guid> orderRepository,
        IRepository<BillOfMaterial, Guid> bomRepository,
        IRepository<Product, Guid> productRepository)
    {
        _orderRepository = orderRepository;
        _bomRepository = bomRepository;
        _productRepository = productRepository;
    }

    public async Task<Result<ProductionOrderDto>> Handle(
        GetProductionOrderByIdQuery query,
        CancellationToken cancellationToken)
    {
        var spec = new ProductionOrderByIdSpec(query.Id);
        var order = await _orderRepository.FirstOrDefaultAsync(spec, cancellationToken);

        if (order is null)
        {
            return Result.Failure<ProductionOrderDto>(
                Error.NotFound($"Production order with ID '{query.Id}' was not found."));
        }

        string? bomCode = null;
        var bomSpec = new BillOfMaterialByIdSpec(order.BillOfMaterialId);
        var bom = await _bomRepository.FirstOrDefaultAsync(bomSpec, cancellationToken);
        if (bom is not null)
        {
            bomCode = bom.Code;
        }

        var productIds = new List<Guid> { order.ProductId };
        productIds.AddRange(order.Items.Select(i => i.ProductId));
        var productNames = await ManufacturingMapper.GetProductNamesAsync(
            _productRepository, productIds, cancellationToken);

        return Result.Success(ManufacturingMapper.ToDto(order, productNames, bomCode));
    }
}
