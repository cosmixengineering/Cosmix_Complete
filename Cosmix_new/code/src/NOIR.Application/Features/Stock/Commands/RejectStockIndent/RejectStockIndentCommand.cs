namespace NOIR.Application.Features.Stock.Commands.RejectStockIndent;

/// <summary>
/// Rejects a Submitted stock indent with an optional reason.
/// </summary>
public sealed record RejectStockIndentCommand(
    Guid Id,
    string? Reason) : IAuditableCommand<StockIndentDto>
{
    [System.Text.Json.Serialization.JsonIgnore]
    public string? UserId { get; init; }

    public AuditOperationType OperationType => AuditOperationType.Update;
    public object? GetTargetId() => Id;
    public string? GetTargetDisplayName() => "Stock Indent";
    public string? GetActionDescription() => "Rejected stock indent";
}

public class RejectStockIndentCommandValidator : AbstractValidator<RejectStockIndentCommand>
{
    public RejectStockIndentCommandValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty().WithMessage("Indent ID is required.");

        RuleFor(x => x.Reason)
            .MaximumLength(1000).WithMessage("Reject reason must not exceed 1000 characters.")
            .When(x => !string.IsNullOrEmpty(x.Reason));
    }
}

public class RejectStockIndentCommandHandler
{
    private readonly IRepository<StockIndent, Guid> _indentRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUser _currentUser;
    private readonly IEntityUpdateHubContext _entityUpdateHub;

    public RejectStockIndentCommandHandler(
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

    public async Task<Result<StockIndentDto>> Handle(RejectStockIndentCommand command, CancellationToken cancellationToken)
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
            indent.Reject(command.Reason);
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
