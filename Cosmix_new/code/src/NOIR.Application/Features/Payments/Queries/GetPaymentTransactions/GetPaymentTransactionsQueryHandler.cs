namespace NOIR.Application.Features.Payments.Queries.GetPaymentTransactions;

/// <summary>
/// Handler for getting a paginated list of payment transactions.
/// </summary>
public class GetPaymentTransactionsQueryHandler
{
    private readonly IRepository<PaymentTransaction, Guid> _paymentRepository;
    private readonly IUserDisplayNameService _userDisplayNameService;

    public GetPaymentTransactionsQueryHandler(IRepository<PaymentTransaction, Guid> paymentRepository, IUserDisplayNameService userDisplayNameService)
    {
        _paymentRepository = paymentRepository;
        _userDisplayNameService = userDisplayNameService;
    }

    public async Task<Result<PagedResult<PaymentTransactionListDto>>> Handle(
        GetPaymentTransactionsQuery query,
        CancellationToken cancellationToken)
    {
        var skip = (query.Page - 1) * query.PageSize;

        var spec = new PaymentTransactionsSpec(
            query.Search,
            query.Status,
            query.PaymentMethod,
            query.Provider,
            skip,
            query.PageSize,
            query.OrderBy,
            query.IsDescending);

        var payments = await _paymentRepository.ListAsync(spec, cancellationToken);

        // Count without pagination
        var countSpec = new PaymentTransactionsSpec(
            query.Search,
            query.Status,
            query.PaymentMethod,
            query.Provider);
        var totalCount = await _paymentRepository.CountAsync(countSpec, cancellationToken);

        // Resolve user names
        var userIds = payments
            .SelectMany(x => new[] { x.CreatedBy, x.ModifiedBy })
            .Where(id => !string.IsNullOrEmpty(id))
            .Select(id => id!)
            .Distinct();
        var userNames = await _userDisplayNameService.GetDisplayNamesAsync(userIds, cancellationToken);

        var items = payments.Select(p => new PaymentTransactionListDto(
            p.Id,
            p.TransactionNumber,
            p.Provider,
            p.Amount,
            p.Currency,
            p.Status,
            p.PaymentMethod,
            p.PaidAt,
            p.CreatedAt,
            p.ModifiedAt,
            p.CreatedBy != null ? userNames.GetValueOrDefault(p.CreatedBy) : null,
            p.ModifiedBy != null ? userNames.GetValueOrDefault(p.ModifiedBy) : null)).ToList();

        var result = PagedResult<PaymentTransactionListDto>.Create(
            items,
            totalCount,
            query.Page - 1,  // Convert 1-based page to 0-based pageIndex
            query.PageSize);

        return Result.Success(result);
    }
}
