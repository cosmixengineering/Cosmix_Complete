namespace NOIR.Application.Features.Products.Queries.GetProducts;

/// <summary>
/// Wolverine handler for getting a list of products.
/// </summary>
public class GetProductsQueryHandler
{
    private readonly IRepository<Product, Guid> _productRepository;
    private readonly IUserDisplayNameService _userDisplayNameService;

    public GetProductsQueryHandler(IRepository<Product, Guid> productRepository, IUserDisplayNameService userDisplayNameService)
    {
        _productRepository = productRepository;
        _userDisplayNameService = userDisplayNameService;
    }

    public async Task<Result<PagedResult<ProductListDto>>> Handle(
        GetProductsQuery query,
        CancellationToken cancellationToken)
    {
        var skip = (query.Page - 1) * query.PageSize;

        // Get products with pagination
        var spec = new ProductsSpec(
            query.Search,
            query.Status,
            query.CategoryId,
            query.Brand,
            query.MinPrice,
            query.MaxPrice,
            query.InStockOnly,
            query.LowStockOnly,
            ProductConstants.LowStockThreshold,
            skip,
            query.PageSize,
            query.AttributeFilters,
            query.OrderBy,
            query.IsDescending);

        var products = await _productRepository.ListAsync(spec, cancellationToken);

        // Get total count for pagination (without skip/take, but with same filters)
        var countSpec = new ProductsCountSpec(
            query.Search,
            query.Status,
            query.CategoryId,
            query.Brand,
            query.MinPrice,
            query.MaxPrice,
            query.InStockOnly,
            query.LowStockOnly,
            ProductConstants.LowStockThreshold,
            query.AttributeFilters);

        var totalCount = await _productRepository.CountAsync(countSpec, cancellationToken);

        // Resolve user names
        var userIds = products
            .SelectMany(x => new[] { x.CreatedBy, x.ModifiedBy })
            .Where(id => !string.IsNullOrEmpty(id))
            .Select(id => id!)
            .Distinct();
        var userNames = await _userDisplayNameService.GetDisplayNamesAsync(userIds, cancellationToken);

        var items = products.Select(p => ProductMapper.ToListDto(p, userNames)).ToList();

        var pageIndex = query.Page - 1;
        var result = PagedResult<ProductListDto>.Create(items, totalCount, pageIndex, query.PageSize);

        return Result.Success(result);
    }
}
