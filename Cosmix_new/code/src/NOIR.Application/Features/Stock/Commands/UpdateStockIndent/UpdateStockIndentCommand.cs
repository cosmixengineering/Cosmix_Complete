namespace NOIR.Application.Features.Stock.Commands.UpdateStockIndent;

/// <summary>
/// Updates the header fields of a Draft stock indent.
/// </summary>
public sealed record UpdateStockIndentCommand(
    Guid Id,
    DateOnly? NeededByDate,
    string? Department,
    string? Notes) : IAuditableCommand<StockIndentDto>
{
    [System.Text.Json.Serialization.JsonIgnore]
    public string? UserId { get; init; }

    public AuditOperationType OperationType => AuditOperationType.Update;
    public object? GetTargetId() => Id;
    public string? GetTargetDisplayName() => "Stock Indent";
    public string? GetActionDescription() => "Updated stock indent";
}

public class UpdateStockIndentCommandValidator : AbstractValidator<UpdateStockIndentCommand>
{
    public UpdateStockIndentCommandValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty().WithMessage("Indent ID is required.");

        RuleFor(x => x.Department)
            .MaximumLength(200).WithMessage("Department must not exceed 200 characters.")
            .When(x => !string.IsNullOrEmpty(x.Department));

        RuleFor(x => x.Notes)
            .MaximumLength(2000).WithMessage("Notes must not exceed 2000 characters.")
            .When(x => !string.IsNullOrEmpty(x.Notes));
    }
}

public class UpdateStockIndentCommandHandler
{
    private readonly IRepository<StockIndent, Guid> _indentRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUser _currentUser;
    private readonly IEntityUpdateHubContext _entityUpdateHub;

    public UpdateStockIndentCommandHandler(
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

    public async Task<Result<StockIndentDto>> Handle(UpdateStockIndentCommand command, CancellationToken cancellationToken)
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
            indent.UpdateHeader(command.NeededByDate, command.Department, command.Notes);
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
