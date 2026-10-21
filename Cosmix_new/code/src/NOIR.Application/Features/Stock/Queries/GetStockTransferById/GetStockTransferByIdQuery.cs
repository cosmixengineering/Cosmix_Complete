namespace NOIR.Application.Features.Stock.Queries.GetStockTransferById;

/// <summary>
/// Full stock transfer detail with items and resolved product names.
/// </summary>
public sealed record GetStockTransferByIdQuery(Guid Id);

public class GetStockTransferByIdQueryHandler
{
    private readonly IReadRepository<StockTransfer, Guid> _transferRepository;
    private readonly IReadRepository<Product, Guid> _productRepository;

    public GetStockTransferByIdQueryHandler(
        IReadRepository<StockTransfer, Guid> transferRepository,
        IReadRepository<Product, Guid> productRepository)
    {
        _transferRepository = transferRepository;
        _productRepository = productRepository;
    }

    public async Task<Result<StockTransferDto>> Handle(GetStockTransferByIdQuery query, CancellationToken cancellationToken)
    {
        var transfer = await _transferRepository.FirstOrDefaultAsync(
            new StockTransferByIdSpec(query.Id),
            cancellationToken);

        if (transfer is null)
        {
            return Result.Failure<StockTransferDto>(
                Error.NotFound($"Stock transfer with ID '{query.Id}' was not found."));
        }

        var productNames = await ResolveProductNamesAsync(transfer, cancellationToken);

        return Result.Success(StockMapper.ToDto(transfer, productNames));
    }

    private async Task<IReadOnlyDictionary<Guid, string?>> ResolveProductNamesAsync(StockTransfer transfer, CancellationToken cancellationToken)
    {
        var productIds = transfer.Items.Select(i => i.ProductId).Distinct().ToList();
        if (productIds.Count == 0)
        {
            return new Dictionary<Guid, string?>();
        }

        var products = await _productRepository.ListAsync(new ProductsByIdsSpec(productIds), cancellationToken);

        return products.ToDictionary(p => p.Id, p => (string?)p.Name);
    }
}
