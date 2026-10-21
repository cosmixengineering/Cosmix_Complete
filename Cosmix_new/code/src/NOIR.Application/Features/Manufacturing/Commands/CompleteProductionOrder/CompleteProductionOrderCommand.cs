namespace NOIR.Application.Features.Manufacturing.Commands.CompleteProductionOrder;

/// <summary>
/// Command to complete an in-progress production order (InProgress → Completed).
/// </summary>
public sealed record CompleteProductionOrderCommand(
    Guid Id,
    string? OrderNumber = null) : IAuditableCommand<ProductionOrderDto>
{
    [System.Text.Json.Serialization.JsonIgnore]
    public string? UserId { get; init; }

    public AuditOperationType OperationType => AuditOperationType.Update;
    public object? GetTargetId() => Id;
    public string? GetTargetDisplayName() => OrderNumber ?? Id.ToString();
    public string? GetActionDescription() => $"Completed production order '{GetTargetDisplayName()}'";
}
