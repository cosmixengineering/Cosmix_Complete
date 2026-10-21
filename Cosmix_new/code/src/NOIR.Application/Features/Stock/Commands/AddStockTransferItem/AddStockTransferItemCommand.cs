namespace NOIR.Application.Features.Stock.Commands.AddStockTransferItem;

/// <summary>
/// Adds a single product line to a Draft stock transfer.
/// </summary>
public sealed record AddStockTransferItemCommand(
    Guid Id,
    Guid ProductId,
    decimal Quantity,
    string Unit,
    string? Remarks) : IAuditableCommand<StockTransferDto>
{
    [System.Text.Json.Serialization.JsonIgnore]
    public string? UserId { get; init; }

    public AuditOperationType OperationType => AuditOperationType.Update;
    public object? GetTargetId() => Id;
    public string? GetTargetDisplayName() => "Stock Transfer";
    public string? GetActionDescription() => "Added item to stock transfer";
}

public class AddStockTransferItemCommandValidator : AbstractValidator<AddStockTransferItemCommand>
{
    public AddStockTransferItemCommandValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty().WithMessage("Transfer ID is required.");

        RuleFor(x => x.ProductId)
            .NotEmpty().WithMessage("Product is required.");

        RuleFor(x => x.Quantity)
            .GreaterThan(0).WithMessage("Quantity must be greater than 0.");

        RuleFor(x => x.Unit)
            .NotEmpty().WithMessage("Unit is required.")
            .MaximumLength(50).WithMessage("Unit must not exceed 50 characters.");

        RuleFor(x => x.Remarks)
            .MaximumLength(1000).WithMessage("Remarks must not exceed 1000 characters.")
            .When(x => !string.IsNullOrEmpty(x.Remarks));
    }
}

public class AddStockTransferItemCommandHandler
{
    private readonly IRepository<StockTransfer, Guid> _transferRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUser _currentUser;
    private readonly IEntityUpdateHubContext _entityUpdateHub;

    public AddStockTransferItemCommandHandler(
        IRepository<StockTransfer, Guid> transferRepository,
        IUnitOfWork unitOfWork,
        ICurrentUser currentUser,
        IEntityUpdateHubContext entityUpdateHub)
    {
        _transferRepository = transferRepository;
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
        _entityUpdateHub = entityUpdateHub;
    }

    public async Task<Result<StockTransferDto>> Handle(AddStockTransferItemCommand command, CancellationToken cancellationToken)
    {
        var transfer = await _transferRepository.FirstOrDefaultAsync(
            new StockTransferByIdForUpdateSpec(command.Id),
            cancellationToken);

        if (transfer is null)
        {
            return Result.Failure<StockTransferDto>(
                Error.NotFound($"Stock transfer with ID '{command.Id}' was not found."));
        }

        try
        {
            transfer.AddItem(StockTransferItem.Create(
                transfer.Id,
                command.ProductId,
                command.Quantity,
                command.Unit,
                command.Remarks,
                _currentUser.TenantId));
        }
        catch (InvalidOperationException ex)
        {
            return Result.Failure<StockTransferDto>(Error.Conflict(ex.Message));
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await _entityUpdateHub.PublishEntityUpdatedAsync(
            entityType: "StockTransfer",
            entityId: transfer.Id,
            operation: EntityOperation.Updated,
            tenantId: _currentUser.TenantId!,
            ct: cancellationToken);

        return Result.Success(StockMapper.ToDto(transfer));
    }
}
