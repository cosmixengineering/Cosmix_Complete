namespace NOIR.Application.Features.Stock.Queries.GetStockIndentById;

/// <summary>
/// Full stock indent detail with items and resolved product names.
/// </summary>
public sealed record GetStockIndentByIdQuery(Guid Id);

public class GetStockIndentByIdQueryHandler
{
    private readonly IReadRepository<StockIndent, Guid> _indentRepository;
    private readonly IReadRepository<Product, Guid> _productRepository;

    public GetStockIndentByIdQueryHandler(
        IReadRepository<StockIndent, Guid> indentRepository,
        IReadRepository<Product, Guid> productRepository)
    {
        _indentRepository = indentRepository;
        _productRepository = productRepository;
    }

    public async Task<Result<StockIndentDto>> Handle(GetStockIndentByIdQuery query, CancellationToken cancellationToken)
    {
        var indent = await _indentRepository.FirstOrDefaultAsync(
            new StockIndentByIdSpec(query.Id),
            cancellationToken);

        if (indent is null)
        {
            return Result.Failure<StockIndentDto>(
                Error.NotFound($"Stock indent with ID '{query.Id}' was not found."));
        }

        var productNames = await ResolveProductNamesAsync(indent, cancellationToken);

        return Result.Success(StockMapper.ToDto(indent, productNames));
    }

    private async Task<IReadOnlyDictionary<Guid, string?>> ResolveProductNamesAsync(StockIndent indent, CancellationToken cancellationToken)
    {
        var productIds = indent.Items.Select(i => i.ProductId).Distinct().ToList();
        if (productIds.Count == 0)
        {
            return new Dictionary<Guid, string?>();
        }

        var products = await _productRepository.ListAsync(new ProductsByIdsSpec(productIds), cancellationToken);

        return products.ToDictionary(p => p.Id, p => (string?)p.Name);
    }
}
