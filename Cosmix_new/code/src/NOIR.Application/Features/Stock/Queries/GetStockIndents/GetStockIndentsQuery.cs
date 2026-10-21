namespace NOIR.Application.Features.Stock.Queries.GetStockIndents;

/// <summary>
/// Paged, filtered list of stock indents.
/// </summary>
public sealed record GetStockIndentsQuery(
    string? Search = null,
    IndentStatus? Status = null,
    int Page = 1,
    int PageSize = 20);

public class GetStockIndentsQueryHandler
{
    private readonly IReadRepository<StockIndent, Guid> _indentRepository;

    public GetStockIndentsQueryHandler(IReadRepository<StockIndent, Guid> indentRepository)
    {
        _indentRepository = indentRepository;
    }

    public async Task<Result<PagedResult<StockIndentListDto>>> Handle(GetStockIndentsQuery query, CancellationToken cancellationToken)
    {
        var page = Math.Max(query.Page, 1);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);

        var listSpec = new StockIndentsByFilterSpec(query.Search, query.Status, page, pageSize);
        var countSpec = new StockIndentsByFilterSpec(query.Search, query.Status);

        var indents = await _indentRepository.ListAsync(listSpec, cancellationToken);
        var totalCount = await _indentRepository.CountAsync(countSpec, cancellationToken);

        return Result.Success(PagedResult<StockIndentListDto>.Create(
            indents.Select(StockMapper.ToListDto).ToList(),
            totalCount,
            page - 1,
            pageSize));
    }
}
