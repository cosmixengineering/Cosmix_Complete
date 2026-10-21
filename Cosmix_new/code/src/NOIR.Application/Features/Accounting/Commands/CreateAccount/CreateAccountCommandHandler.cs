namespace NOIR.Application.Features.Accounting.Commands.CreateAccount;

/// <summary>
/// Wolverine handler for creating a new account.
/// </summary>
public class CreateAccountCommandHandler
{
    private readonly IRepository<Account, Guid> _accountRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUser _currentUser;
    private readonly IEntityUpdateHubContext _entityUpdateHub;

    public CreateAccountCommandHandler(
        IRepository<Account, Guid> accountRepository,
        IUnitOfWork unitOfWork,
        ICurrentUser currentUser,
        IEntityUpdateHubContext entityUpdateHub)
    {
        _accountRepository = accountRepository;
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
        _entityUpdateHub = entityUpdateHub;
    }

    public async Task<Result<AccountDto>> Handle(
        CreateAccountCommand command,
        CancellationToken cancellationToken)
    {
        var tenantId = _currentUser.TenantId;

        // Check if account code already exists
        var codeSpec = new AccountCodeExistsSpec(command.Code);
        var existingAccount = await _accountRepository.FirstOrDefaultAsync(codeSpec, cancellationToken);
        if (existingAccount != null)
        {
            return Result.Failure<AccountDto>(
                Error.Conflict($"An account with code '{command.Code}' already exists.", ErrorCodes.Business.AlreadyExists));
        }

        // Validate parent account exists when specified
        if (command.ParentId.HasValue)
        {
            var parentSpec = new AccountByIdSpec(command.ParentId.Value);
            var parent = await _accountRepository.FirstOrDefaultAsync(parentSpec, cancellationToken);
            if (parent == null)
            {
                return Result.Failure<AccountDto>(
                    Error.Validation("ParentId", $"Parent account with ID '{command.ParentId}' was not found."));
            }
        }

        // Create the account (never a system account via API)
        var account = Account.Create(
            command.Code,
            command.Name,
            command.Type,
            command.ParentId,
            isSystem: false,
            command.Description,
            tenantId);

        await _accountRepository.AddAsync(account, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await _entityUpdateHub.PublishEntityUpdatedAsync(
            entityType: "Account",
            entityId: account.Id,
            operation: EntityOperation.Created,
            tenantId: _currentUser.TenantId!,
            cancellationToken);

        return Result.Success(AccountingMapper.ToDto(account));
    }
}
