namespace NOIR.Application.Features.Stock.Commands.AddStockIndentItem;

/// <summary>
/// Adds a single product line to a Draft stock indent.
/// </summary>
public sealed record AddStockIndentItemCommand(
    Guid Id,
    Guid ProductId,
    decimal Quantity,
    string Unit,
    string? Remarks) : IAuditableCommand<StockIndentDto>
{
    [System.Text.Json.Serialization.JsonIgnore]
    public string? UserId { get; init; }

    public AuditOperationType OperationType => AuditOperationType.Update;
    public object? GetTargetId() => Id;
    public string? GetTargetDisplayName() => "Stock Indent";
    public string? GetActionDescription() => "Added item to stock indent";
}

public class AddStockIndentItemCommandValidator : AbstractValidator<AddStockIndentItemCommand>
{
    public AddStockIndentItemCommandValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty().WithMessage("Indent ID is required.");

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

public class AddStockIndentItemCommandHandler
{
    private readonly IRepository<StockIndent, Guid> _indentRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUser _currentUser;
    private readonly IEntityUpdateHubContext _entityUpdateHub;

    public AddStockIndentItemCommandHandler(
        IRepository<StockIndent, Guid> indentRepository,
        IUnitOfWork unitOfWork,
        ICurrentUser currentUser,
        IEntityUpdateHubContext entityUpdateHub)
    {
        _indentRepository = indentRepository;
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
        _entityUpdateHub = entityUpdateHub;
    }

    public async Task<Result<StockIndentDto>> Handle(AddStockIndentItemCommand command, CancellationToken cancellationToken)
    {
        var indent = await _indentRepository.FirstOrDefaultAsync(
            new StockIndentByIdForUpdateSpec(command.Id),
            cancellationToken);

        if (indent is null)
        {
            return Result.Failure<StockIndentDto>(
                Error.NotFound($"Stock indent with ID '{command.Id}' was not found."));
        }

        try
        {
            indent.AddItem(StockIndentItem.Create(
                indent.Id,
                command.ProductId,
                command.Quantity,
                command.Unit,
                command.Remarks,
                _currentUser.TenantId));
        }
        catch (InvalidOperationException ex)
        {
            return Result.Failure<StockIndentDto>(Error.Conflict(ex.Message));
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await _entityUpdateHub.PublishEntityUpdatedAsync(
            entityType: "StockIndent",
            entityId: indent.Id,
            operation: EntityOperation.Updated,
            tenantId: _currentUser.TenantId!,
            ct: cancellationToken);

        return Result.Success(StockMapper.ToDto(indent));
    }
}
