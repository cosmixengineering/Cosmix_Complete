namespace NOIR.Application.Features.Accounting.Queries.GetAccounts;

/// <summary>
/// Wolverine handler for getting paged list of accounts.
/// </summary>
public class GetAccountsQueryHandler
{
    private readonly IRepository<Account, Guid> _accountRepository;

    public GetAccountsQueryHandler(IRepository<Account, Guid> accountRepository)
    {
        _accountRepository = accountRepository;
    }

    public async Task<Result<PagedResult<AccountListDto>>> Handle(
        GetAccountsQuery query,
        CancellationToken cancellationToken)
    {
        // Get paged accounts
        var spec = new AccountsPagedSpec(
            query.Search,
            query.Type,
            query.IsActive,
            query.Page,
            query.PageSize);

        var accounts = await _accountRepository.ListAsync(spec, cancellationToken);

        // Get total count
        var countSpec = new AccountsCountSpec(
            query.Search,
            query.Type,
            query.IsActive);

        var totalCount = await _accountRepository.CountAsync(countSpec, cancellationToken);

        // Resolve child counts in a single extra query
        var childCounts = new Dictionary<Guid, int>();
        var accountIds = accounts.Select(a => a.Id).ToList();

        if (accountIds.Count > 0)
        {
            var childrenSpec = new AccountsByParentIdsSpec(accountIds);
            var children = await _accountRepository.ListAsync(childrenSpec, cancellationToken);

            childCounts = children
                .GroupBy(c => c.ParentId!.Value)
                .ToDictionary(g => g.Key, g => g.Count());
        }

        var items = accounts
            .Select(a => AccountingMapper.ToListDto(a, childCounts.GetValueOrDefault(a.Id)))
            .ToList();

        // PagedResult expects pageIndex (0-based), but query.Page is 1-based
        var pageIndex = query.Page - 1;

        return Result.Success(PagedResult<AccountListDto>.Create(
            items,
            totalCount,
            pageIndex,
            query.PageSize));
    }
}
