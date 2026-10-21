namespace NOIR.Application.Features.Estimation.Queries.GetBoqs;

/// <summary>
/// Wolverine handler for getting paged list of BOQs.
/// </summary>
public class GetBoqsQueryHandler
{
    private readonly IRepository<Boq, Guid> _boqRepository;

    public GetBoqsQueryHandler(IRepository<Boq, Guid> boqRepository)
    {
        _boqRepository = boqRepository;
    }

    public async Task<Result<PagedResult<BoqListDto>>> Handle(
        GetBoqsQuery query,
        CancellationToken cancellationToken)
    {
        // Get paged BOQs
        var spec = new BoqsByFilterSpec(
            query.Search,
            query.Status,
            query.Page,
            query.PageSize);

        var boqs = await _boqRepository.ListAsync(spec, cancellationToken);

        // Get total count
        var countSpec = new BoqsCountSpec(
            query.Search,
            query.Status);

        var totalCount = await _boqRepository.CountAsync(countSpec, cancellationToken);

        var items = boqs.Select(BoqMapper.ToListDto).ToList();

        // PagedResult expects pageIndex (0-based), but query.Page is 1-based
        var pageIndex = query.Page - 1;

        return Result.Success(PagedResult<BoqListDto>.Create(
            items,
            totalCount,
            pageIndex,
            query.PageSize));
    }
}
