namespace NOIR.Application.Features.Stock.Commands.UpdateStockTransfer;

/// <summary>
/// Updates the header fields of a Draft stock transfer.
/// </summary>
public sealed record UpdateStockTransferCommand(
    Guid Id,
    string? FromLocation,
    string? ToLocation,
    DateOnly? TransferDate,
    string? Notes) : IAuditableCommand<StockTransferDto>
{
    [System.Text.Json.Serialization.JsonIgnore]
    public string? UserId { get; init; }

    public AuditOperationType OperationType => AuditOperationType.Update;
    public object? GetTargetId() => Id;
    public string? GetTargetDisplayName() => "Stock Transfer";
    public string? GetActionDescription() => "Updated stock transfer";
}

public class UpdateStockTransferCommandValidator : AbstractValidator<UpdateStockTransferCommand>
{
    public UpdateStockTransferCommandValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty().WithMessage("Transfer ID is required.");

        RuleFor(x => x.FromLocation)
            .MaximumLength(200).WithMessage("From location must not exceed 200 characters.")
            .When(x => !string.IsNullOrEmpty(x.FromLocation));

        RuleFor(x => x.ToLocation)
            .MaximumLength(200).WithMessage("To location must not exceed 200 characters.")
            .When(x => !string.IsNullOrEmpty(x.ToLocation));

        RuleFor(x => x.Notes)
            .MaximumLength(2000).WithMessage("Notes must not exceed 2000 characters.")
            .When(x => !string.IsNullOrEmpty(x.Notes));
    }
}

public class UpdateStockTransferCommandHandler
{
    private readonly IRepository<StockTransfer, Guid> _transferRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUser _currentUser;
    private readonly IEntityUpdateHubContext _entityUpdateHub;

    public UpdateStockTransferCommandHandler(
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

    public async Task<Result<StockTransferDto>> Handle(UpdateStockTransferCommand command, CancellationToken cancellationToken)
    {
        var transfer = await _transferRepository.FirstOrDefaultAsync(
            new StockTransferByIdForUpdateSpec(command.Id),
            cancellationToken);

        if (transfer is null)
        {
            return Result.Failure<StockTransferDto>(
                Error.NotFound($"Stock transfer with ID '{command.Id}' was not found."));
        }

        var fromLocation = string.IsNullOrWhiteSpace(command.FromLocation) ? transfer.FromLocation : command.FromLocation;
        var toLocation = string.IsNullOrWhiteSpace(command.ToLocation) ? transfer.ToLocation : command.ToLocation;

        if (string.Equals(fromLocation, toLocation, StringComparison.OrdinalIgnoreCase))
        {
            return Result.Failure<StockTransferDto>(
                Error.Conflict("From and to locations must be different."));
        }

        try
        {
            transfer.UpdateHeader(command.FromLocation, command.ToLocation, command.TransferDate, command.Notes);
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
