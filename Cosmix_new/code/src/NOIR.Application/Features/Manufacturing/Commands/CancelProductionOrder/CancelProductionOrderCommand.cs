namespace NOIR.Application.Features.Manufacturing.Commands.CancelProductionOrder;

/// <summary>
/// Command to cancel a production order (Draft/Released/InProgress → Cancelled).
/// </summary>
public sealed record CancelProductionOrderCommand(
    Guid Id,
    string? Reason,
    string? OrderNumber = null) : IAuditableCommand<ProductionOrderDto>
{
    [System.Text.Json.Serialization.JsonIgnore]
    public string? UserId { get; init; }

    public AuditOperationType OperationType => AuditOperationType.Update;
    public object? GetTargetId() => Id;
    public string? GetTargetDisplayName() => OrderNumber ?? Id.ToString();
    public string? GetActionDescription() => $"Cancelled production order '{GetTargetDisplayName()}'";
}
