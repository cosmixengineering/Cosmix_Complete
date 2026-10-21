namespace NOIR.Application.Features.Stock.Queries.GetStockTransfers;

/// <summary>
/// Paged, filtered list of stock transfers.
/// </summary>
public sealed record GetStockTransfersQuery(
    string? Search = null,
    TransferStatus? Status = null,
    string? FromLocation = null,
    string? ToLocation = null,
    int Page = 1,
    int PageSize = 20);

public class GetStockTransfersQueryHandler
{
    private readonly IReadRepository<StockTransfer, Guid> _transferRepository;

    public GetStockTransfersQueryHandler(IReadRepository<StockTransfer, Guid> transferRepository)
    {
        _transferRepository = transferRepository;
    }

    public async Task<Result<PagedResult<StockTransferListDto>>> Handle(GetStockTransfersQuery query, CancellationToken cancellationToken)
    {
        var page = Math.Max(query.Page, 1);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);

        var listSpec = new StockTransfersByFilterSpec(
            query.Search, query.Status, query.FromLocation, query.ToLocation, page, pageSize);
        var countSpec = new StockTransfersByFilterSpec(
            query.Search, query.Status, query.FromLocation, query.ToLocation);

        var transfers = await _transferRepository.ListAsync(listSpec, cancellationToken);
        var totalCount = await _transferRepository.CountAsync(countSpec, cancellationToken);

        return Result.Success(PagedResult<StockTransferListDto>.Create(
            transfers.Select(StockMapper.ToListDto).ToList(),
            totalCount,
            page - 1,
            pageSize));
    }
}
