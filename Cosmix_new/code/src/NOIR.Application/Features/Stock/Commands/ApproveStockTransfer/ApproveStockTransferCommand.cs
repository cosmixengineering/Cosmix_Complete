namespace NOIR.Application.Features.Stock.Commands.ApproveStockTransfer;

/// <summary>
/// Approves a Draft or Submitted stock transfer.
/// </summary>
public sealed record ApproveStockTransferCommand(Guid Id) : IAuditableCommand<StockTransferDto>
{
    [System.Text.Json.Serialization.JsonIgnore]
    public string? UserId { get; init; }

    public AuditOperationType OperationType => AuditOperationType.Update;
    public object? GetTargetId() => Id;
    public string? GetTargetDisplayName() => "Stock Transfer";
    public string? GetActionDescription() => "Approved stock transfer";
}

public class ApproveStockTransferCommandValidator : AbstractValidator<ApproveStockTransferCommand>
{
    public ApproveStockTransferCommandValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty().WithMessage("Transfer ID is required.");
    }
}

public class ApproveStockTransferCommandHandler
{
    private readonly IRepository<StockTransfer, Guid> _transferRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUser _currentUser;
    private readonly IEntityUpdateHubContext _entityUpdateHub;

    public ApproveStockTransferCommandHandler(
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

    public async Task<Result<StockTransferDto>> Handle(ApproveStockTransferCommand command, CancellationToken cancellationToken)
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
            transfer.Approve(Guid.TryParse(_currentUser.UserId, out var approvedByUserId) ? approvedByUserId : null);
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
