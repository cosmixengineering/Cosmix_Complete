namespace NOIR.Application.Features.Manufacturing.Commands.CreateProductionOrder;

/// <summary>
/// Command to create a new production order (Draft) with a material requirements snapshot from the BOM.
/// </summary>
public sealed record CreateProductionOrderCommand(
    string OrderNumber,
    Guid BillOfMaterialId,
    decimal PlannedQuantity,
    DateOnly? PlannedStartDate,
    DateOnly? PlannedEndDate,
    string? Notes) : IAuditableCommand<ProductionOrderDto>
{
    [System.Text.Json.Serialization.JsonIgnore]
    public string? UserId { get; init; }

    public AuditOperationType OperationType => AuditOperationType.Create;
    public object? GetTargetId() => null;
    public string? GetTargetDisplayName() => OrderNumber;
    public string? GetActionDescription() => $"Created production order '{OrderNumber}'";
}
