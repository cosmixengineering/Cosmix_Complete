namespace NOIR.Application.Features.Stock.Commands.CancelStockIndent;

/// <summary>
/// Cancels a Draft or Submitted stock indent.
/// </summary>
public sealed record CancelStockIndentCommand(Guid Id) : IAuditableCommand<StockIndentDto>
{
    [System.Text.Json.Serialization.JsonIgnore]
    public string? UserId { get; init; }

    public AuditOperationType OperationType => AuditOperationType.Update;
    public object? GetTargetId() => Id;
    public string? GetTargetDisplayName() => "Stock Indent";
    public string? GetActionDescription() => "Cancelled stock indent";
}

public class CancelStockIndentCommandValidator : AbstractValidator<CancelStockIndentCommand>
{
    public CancelStockIndentCommandValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty().WithMessage("Indent ID is required.");
    }
}

public class CancelStockIndentCommandHandler
{
    private readonly IRepository<StockIndent, Guid> _indentRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUser _currentUser;
    private readonly IEntityUpdateHubContext _entityUpdateHub;

    public CancelStockIndentCommandHandler(
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

    public async Task<Result<StockIndentDto>> Handle(CancelStockIndentCommand command, CancellationToken cancellationToken)
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
            indent.Cancel();
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
