namespace NOIR.Application.Features.Manufacturing.Commands.StartProductionOrder;

/// <summary>
/// Command to start a released production order (Released → InProgress).
/// </summary>
public sealed record StartProductionOrderCommand(
    Guid Id,
    string? OrderNumber = null) : IAuditableCommand<ProductionOrderDto>
{
    [System.Text.Json.Serialization.JsonIgnore]
    public string? UserId { get; init; }

    public AuditOperationType OperationType => AuditOperationType.Update;
    public object? GetTargetId() => Id;
    public string? GetTargetDisplayName() => OrderNumber ?? Id.ToString();
    public string? GetActionDescription() => $"Started production order '{GetTargetDisplayName()}'";
}
