namespace NOIR.Application.Features.Manufacturing.Commands.IssueMaterial;

/// <summary>
/// Command to issue (consume) material against a production order requirement line.
/// </summary>
public sealed record IssueMaterialCommand(
    Guid ProductionOrderId,
    Guid ProductionOrderItemId,
    decimal Quantity) : IAuditableCommand<ProductionOrderDto>
{
    [System.Text.Json.Serialization.JsonIgnore]
    public string? UserId { get; init; }

    public AuditOperationType OperationType => AuditOperationType.Update;
    public object? GetTargetId() => ProductionOrderId;
    public string? GetTargetDisplayName() => null;
    public string? GetActionDescription() => $"Issued {Quantity} of material on production order";
}
