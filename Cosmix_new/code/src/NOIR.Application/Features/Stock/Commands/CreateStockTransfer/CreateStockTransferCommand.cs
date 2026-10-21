namespace NOIR.Application.Features.Stock.Commands.CreateStockTransfer;

/// <summary>
/// Creates a new stock transfer in Draft status with its initial items.
/// </summary>
public sealed record CreateStockTransferCommand(
    string TransferNumber,
    string FromLocation,
    string ToLocation,
    DateOnly? TransferDate,
    string? Notes,
    IReadOnlyList<StockTransferItemRequest> Items) : IAuditableCommand<StockTransferDto>
{
    [System.Text.Json.Serialization.JsonIgnore]
    public string? UserId { get; init; }

    public AuditOperationType OperationType => AuditOperationType.Create;
    public object? GetTargetId() => null;
    public string? GetTargetDisplayName() => TransferNumber;
    public string? GetActionDescription() => $"Created stock transfer '{TransferNumber}'";
}

public class CreateStockTransferCommandValidator : AbstractValidator<CreateStockTransferCommand>
{
    public CreateStockTransferCommandValidator()
    {
        RuleFor(x => x.TransferNumber)
            .NotEmpty().WithMessage("Transfer number is required.")
            .MaximumLength(50).WithMessage("Transfer number must not exceed 50 characters.");

        RuleFor(x => x.FromLocation)
            .NotEmpty().WithMessage("From location is required.")
            .MaximumLength(200).WithMessage("From location must not exceed 200 characters.");

        RuleFor(x => x.ToLocation)
            .NotEmpty().WithMessage("To location is required.")
            .MaximumLength(200).WithMessage("To location must not exceed 200 characters.");

        RuleFor(x => x)
            .Must(x => !string.Equals(x.FromLocation, x.ToLocation, StringComparison.OrdinalIgnoreCase))
            .WithMessage("From and to locations must be different.");

        RuleFor(x => x.Notes)
            .MaximumLength(2000).WithMessage("Notes must not exceed 2000 characters.")
            .When(x => !string.IsNullOrEmpty(x.Notes));

        RuleFor(x => x.Items)
            .NotEmpty().WithMessage("At least one item is required.");

        RuleForEach(x => x.Items).ChildRules(item =>
        {
            item.RuleFor(i => i.ProductId)
                .NotEmpty().WithMessage("Product is required.");

            item.RuleFor(i => i.Quantity)
                .GreaterThan(0).WithMessage("Quantity must be greater than 0.");

            item.RuleFor(i => i.Unit)
                .NotEmpty().WithMessage("Unit is required.")
                .MaximumLength(50).WithMessage("Unit must not exceed 50 characters.");

            item.RuleFor(i => i.Remarks)
                .MaximumLength(1000).WithMessage("Remarks must not exceed 1000 characters.")
                .When(i => !string.IsNullOrEmpty(i.Remarks));
        });
    }
}

public class CreateStockTransferCommandHandler
{
    private readonly IRepository<StockTransfer, Guid> _transferRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUser _currentUser;
    private readonly IEntityUpdateHubContext _entityUpdateHub;

    public CreateStockTransferCommandHandler(
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

    public async Task<Result<StockTransferDto>> Handle(CreateStockTransferCommand command, CancellationToken cancellationToken)
    {
        var numberSpec = new StockTransferNumberExistsSpec(command.TransferNumber);
        if (await _transferRepository.AnyAsync(numberSpec, cancellationToken))
        {
            return Result.Failure<StockTransferDto>(
                Error.Conflict($"A stock transfer with number '{command.TransferNumber}' already exists."));
        }

        var transfer = StockTransfer.Create(
            command.TransferNumber,
            command.FromLocation,
            command.ToLocation,
            command.TransferDate,
            command.Notes,
            _currentUser.TenantId);

        try
        {
            foreach (var item in command.Items)
            {
                transfer.AddItem(StockTransferItem.Create(
                    transfer.Id,
                    item.ProductId,
                    item.Quantity,
                    item.Unit,
                    item.Remarks,
                    _currentUser.TenantId));
            }
        }
        catch (InvalidOperationException ex)
        {
            return Result.Failure<StockTransferDto>(Error.Conflict(ex.Message));
        }

        await _transferRepository.AddAsync(transfer, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await _entityUpdateHub.PublishEntityUpdatedAsync(
            entityType: "StockTransfer",
            entityId: transfer.Id,
            operation: EntityOperation.Created,
            tenantId: _currentUser.TenantId!,
            ct: cancellationToken);

        return Result.Success(StockMapper.ToDto(transfer));
    }
}
