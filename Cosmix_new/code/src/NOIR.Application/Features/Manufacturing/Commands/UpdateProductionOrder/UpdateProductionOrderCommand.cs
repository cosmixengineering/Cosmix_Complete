namespace NOIR.Application.Features.Manufacturing.Commands.UpdateProductionOrder;

/// <summary>
/// Command to update a draft production order header.
/// Rescales the material requirements snapshot when the planned quantity changes.
/// </summary>
public sealed record UpdateProductionOrderCommand(
    Guid Id,
    decimal PlannedQuantity,
    DateOnly? PlannedStartDate,
    DateOnly? PlannedEndDate,
    string? Notes) : IAuditableCommand<ProductionOrderDto>
{
    [System.Text.Json.Serialization.JsonIgnore]
    public string? UserId { get; init; }

    public AuditOperationType OperationType => AuditOperationType.Update;
    public object? GetTargetId() => Id;
    public string? GetTargetDisplayName() => null;
    public string? GetActionDescription() => "Updated production order";
}
