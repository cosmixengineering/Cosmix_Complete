namespace NOIR.Application.Features.Manufacturing.Queries.GetBillOfMaterials;

/// <summary>
/// Wolverine handler for getting paged list of bills of material.
/// </summary>
public class GetBillOfMaterialsQueryHandler
{
    private readonly IRepository<BillOfMaterial, Guid> _bomRepository;

    public GetBillOfMaterialsQueryHandler(IRepository<BillOfMaterial, Guid> bomRepository)
    {
        _bomRepository = bomRepository;
    }

    public async Task<Result<PagedResult<BillOfMaterialListDto>>> Handle(
        GetBillOfMaterialsQuery query,
        CancellationToken cancellationToken)
    {
        var spec = new BillOfMaterialsByFilterSpec(
            query.Search,
            query.IsActive,
            query.Page,
            query.PageSize);

        var boms = await _bomRepository.ListAsync(spec, cancellationToken);

        var countSpec = new BillOfMaterialsCountSpec(query.Search, query.IsActive);
        var totalCount = await _bomRepository.CountAsync(countSpec, cancellationToken);

        var items = boms.Select(ManufacturingMapper.ToListDto).ToList();

        return Result.Success(PagedResult<BillOfMaterialListDto>.Create(
            items,
            totalCount,
            query.Page - 1,
            query.PageSize));
    }
}
