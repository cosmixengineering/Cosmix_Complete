namespace NOIR.Application.Features.Stock.Commands.CreateStockIndent;

/// <summary>
/// Creates a new stock indent in Draft status with its initial items.
/// </summary>
public sealed record CreateStockIndentCommand(
    string IndentNumber,
    DateOnly? NeededByDate,
    string? Department,
    string? Notes,
    IReadOnlyList<StockIndentItemRequest> Items) : IAuditableCommand<StockIndentDto>
{
    [System.Text.Json.Serialization.JsonIgnore]
    public string? UserId { get; init; }

    public AuditOperationType OperationType => AuditOperationType.Create;
    public object? GetTargetId() => null;
    public string? GetTargetDisplayName() => IndentNumber;
    public string? GetActionDescription() => $"Created stock indent '{IndentNumber}'";
}

public class CreateStockIndentCommandValidator : AbstractValidator<CreateStockIndentCommand>
{
    public CreateStockIndentCommandValidator()
    {
        RuleFor(x => x.IndentNumber)
            .NotEmpty().WithMessage("Indent number is required.")
            .MaximumLength(50).WithMessage("Indent number must not exceed 50 characters.");

        RuleFor(x => x.Department)
            .MaximumLength(200).WithMessage("Department must not exceed 200 characters.")
            .When(x => !string.IsNullOrEmpty(x.Department));

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

public class CreateStockIndentCommandHandler
{
    private readonly IRepository<StockIndent, Guid> _indentRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUser _currentUser;
    private readonly IEntityUpdateHubContext _entityUpdateHub;

    public CreateStockIndentCommandHandler(
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

    public async Task<Result<StockIndentDto>> Handle(CreateStockIndentCommand command, CancellationToken cancellationToken)
    {
        var numberSpec = new StockIndentNumberExistsSpec(command.IndentNumber);
        if (await _indentRepository.AnyAsync(numberSpec, cancellationToken))
        {
            return Result.Failure<StockIndentDto>(
                Error.Conflict($"A stock indent with number '{command.IndentNumber}' already exists."));
        }

        var indent = StockIndent.Create(
            command.IndentNumber,
            Guid.TryParse(_currentUser.UserId, out var requestedByUserId) ? requestedByUserId : null,
            command.NeededByDate,
            command.Department,
            command.Notes,
            _currentUser.TenantId);

        try
        {
            foreach (var item in command.Items)
            {
                indent.AddItem(StockIndentItem.Create(
                    indent.Id,
                    item.ProductId,
                    item.Quantity,
                    item.Unit,
                    item.Remarks,
                    _currentUser.TenantId));
            }
        }
        catch (InvalidOperationException ex)
        {
            return Result.Failure<StockIndentDto>(Error.Conflict(ex.Message));
        }

        await _indentRepository.AddAsync(indent, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await _entityUpdateHub.PublishEntityUpdatedAsync(
            entityType: "StockIndent",
            entityId: indent.Id,
            operation: EntityOperation.Created,
            tenantId: _currentUser.TenantId!,
            ct: cancellationToken);

        return Result.Success(StockMapper.ToDto(indent));
    }
}
