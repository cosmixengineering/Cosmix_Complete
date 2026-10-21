namespace NOIR.Application.Features.Manufacturing.Queries.GetBillOfMaterialById;

/// <summary>
/// Wolverine handler for getting a bill of material by ID.
/// </summary>
public class GetBillOfMaterialByIdQueryHandler
{
    private readonly IRepository<BillOfMaterial, Guid> _bomRepository;
    private readonly IRepository<Product, Guid> _productRepository;

    public GetBillOfMaterialByIdQueryHandler(
        IRepository<BillOfMaterial, Guid> bomRepository,
        IRepository<Product, Guid> productRepository)
    {
        _bomRepository = bomRepository;
        _productRepository = productRepository;
    }

    public async Task<Result<BillOfMaterialDto>> Handle(
        GetBillOfMaterialByIdQuery query,
        CancellationToken cancellationToken)
    {
        var spec = new BillOfMaterialByIdSpec(query.Id);
        var bom = await _bomRepository.FirstOrDefaultAsync(spec, cancellationToken);

        if (bom is null)
        {
            return Result.Failure<BillOfMaterialDto>(
                Error.NotFound($"Bill of material with ID '{query.Id}' was not found."));
        }

        var productIds = new List<Guid> { bom.ProductId };
        productIds.AddRange(bom.Items.Select(i => i.ProductId));
        var productNames = await ManufacturingMapper.GetProductNamesAsync(
            _productRepository, productIds, cancellationToken);

        return Result.Success(ManufacturingMapper.ToDto(bom, productNames));
    }
}
