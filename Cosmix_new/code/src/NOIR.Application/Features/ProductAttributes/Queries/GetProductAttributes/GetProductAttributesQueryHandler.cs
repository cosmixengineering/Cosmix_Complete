namespace NOIR.Application.Features.ProductAttributes.Queries.GetProductAttributes;

/// <summary>
/// Wolverine handler for getting paged list of product attributes.
/// </summary>
public class GetProductAttributesQueryHandler
{
    private readonly IRepository<ProductAttribute, Guid> _attributeRepository;
    private readonly IUserDisplayNameService _userDisplayNameService;

    public GetProductAttributesQueryHandler(IRepository<ProductAttribute, Guid> attributeRepository, IUserDisplayNameService userDisplayNameService)
    {
        _attributeRepository = attributeRepository;
        _userDisplayNameService = userDisplayNameService;
    }

    public async Task<Result<PagedResult<ProductAttributeListDto>>> Handle(
        GetProductAttributesQuery query,
        CancellationToken cancellationToken)
    {
        // Get paged attributes
        var spec = new ProductAttributesPagedSpec(
            query.Search,
            query.IsActive,
            query.IsFilterable,
            query.IsVariantAttribute,
            query.Type,
            query.Page,
            query.PageSize,
            includeValues: true,
            query.OrderBy,
            query.IsDescending);

        var attributes = await _attributeRepository.ListAsync(spec, cancellationToken);

        // Get total count
        var countSpec = new ProductAttributesCountSpec(
            query.Search,
            query.IsActive,
            query.IsFilterable,
            query.IsVariantAttribute,
            query.Type);

        var totalCount = await _attributeRepository.CountAsync(countSpec, cancellationToken);

        // Resolve user names
        var userIds = attributes
            .SelectMany(x => new[] { x.CreatedBy, x.ModifiedBy })
            .Where(id => !string.IsNullOrEmpty(id))
            .Select(id => id!)
            .Distinct();
        var userNames = await _userDisplayNameService.GetDisplayNamesAsync(userIds, cancellationToken);

        var items = attributes.Select(a => ProductAttributeMapper.ToListDto(a, userNames)).ToList();

        // PagedResult expects pageIndex (0-based), but query.Page is 1-based
        var pageIndex = query.Page - 1;

        return Result.Success(PagedResult<ProductAttributeListDto>.Create(
            items,
            totalCount,
            pageIndex,
            query.PageSize));
    }
}
