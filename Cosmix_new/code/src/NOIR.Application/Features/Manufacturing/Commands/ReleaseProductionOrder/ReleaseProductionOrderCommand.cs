namespace NOIR.Application.Features.Manufacturing.Commands.ReleaseProductionOrder;

/// <summary>
/// Command to release a draft production order (Draft → Released).
/// </summary>
public sealed record ReleaseProductionOrderCommand(
    Guid Id,
    string? OrderNumber = null) : IAuditableCommand<ProductionOrderDto>
{
    [System.Text.Json.Serialization.JsonIgnore]
    public string? UserId { get; init; }

    public AuditOperationType OperationType => AuditOperationType.Update;
    public object? GetTargetId() => Id;
    public string? GetTargetDisplayName() => OrderNumber ?? Id.ToString();
    public string? GetActionDescription() => $"Released production order '{GetTargetDisplayName()}'";
}
