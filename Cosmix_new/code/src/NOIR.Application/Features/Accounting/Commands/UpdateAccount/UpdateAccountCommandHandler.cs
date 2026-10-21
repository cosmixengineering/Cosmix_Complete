namespace NOIR.Application.Features.Accounting.Commands.UpdateAccount;

/// <summary>
/// Wolverine handler for updating an account.
/// </summary>
public class UpdateAccountCommandHandler
{
    private readonly IRepository<Account, Guid> _accountRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUser _currentUser;
    private readonly IEntityUpdateHubContext _entityUpdateHub;

    public UpdateAccountCommandHandler(
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
        UpdateAccountCommand command,
        CancellationToken cancellationToken)
    {
        // Get the account for update
        var spec = new AccountByIdForUpdateSpec(command.Id);
        var account = await _accountRepository.FirstOrDefaultAsync(spec, cancellationToken);

        if (account == null)
        {
            return Result.Failure<AccountDto>(
                Error.NotFound($"Account with ID '{command.Id}' was not found.", ErrorCodes.Business.NotFound));
        }

        // Check if account code already exists (for a different account)
        if (account.Code != command.Code)
        {
            var codeSpec = new AccountCodeExistsSpec(command.Code, command.Id);
            var existingAccount = await _accountRepository.FirstOrDefaultAsync(codeSpec, cancellationToken);
            if (existingAccount != null)
            {
                return Result.Failure<AccountDto>(
                    Error.Conflict($"An account with code '{command.Code}' already exists.", ErrorCodes.Business.AlreadyExists));
            }
        }

        // Prevent self-referencing hierarchy
        if (command.ParentId.HasValue && command.ParentId.Value == command.Id)
        {
            return Result.Failure<AccountDto>(
                Error.Validation("ParentId", "An account cannot be its own parent."));
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

        // Update the account
        account.Update(command.Code, command.Name, command.ParentId, command.Description);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var childCount = await _accountRepository.CountAsync(
            a => a.ParentId == account.Id, cancellationToken);

        await _entityUpdateHub.PublishEntityUpdatedAsync(
            entityType: "Account",
            entityId: account.Id,
            operation: EntityOperation.Updated,
            tenantId: _currentUser.TenantId!,
            cancellationToken);

        return Result.Success(AccountingMapper.ToDto(account, childCount));
    }
}
