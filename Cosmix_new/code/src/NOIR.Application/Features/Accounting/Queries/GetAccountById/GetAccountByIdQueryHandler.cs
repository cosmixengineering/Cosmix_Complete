namespace NOIR.Application.Features.Accounting.Queries.GetAccountById;

/// <summary>
/// Wolverine handler for getting an account by ID.
/// </summary>
public class GetAccountByIdQueryHandler
{
    private readonly IRepository<Account, Guid> _accountRepository;

    public GetAccountByIdQueryHandler(IRepository<Account, Guid> accountRepository)
    {
        _accountRepository = accountRepository;
    }

    public async Task<Result<AccountDto>> Handle(
        GetAccountByIdQuery query,
        CancellationToken cancellationToken)
    {
        var spec = new AccountByIdSpec(query.Id);
        var account = await _accountRepository.FirstOrDefaultAsync(spec, cancellationToken);

        if (account == null)
        {
            return Result.Failure<AccountDto>(
                Error.NotFound($"Account with ID '{query.Id}' was not found.", ErrorCodes.Business.NotFound));
        }

        var childCount = await _accountRepository.CountAsync(
            a => a.ParentId == account.Id, cancellationToken);

        return Result.Success(AccountingMapper.ToDto(account, childCount));
    }
}
