namespace NOIR.Application.Features.Manufacturing.Commands.AddBillOfMaterialItem;

/// <summary>
/// Command to add a component item to an existing bill of material.
/// </summary>
public sealed record AddBillOfMaterialItemCommand(
    Guid BillOfMaterialId,
    Guid ProductId,
    decimal Quantity,
    string Unit,
    decimal? WastePercent,
    string? Notes) : IAuditableCommand<BillOfMaterialDto>
{
    [System.Text.Json.Serialization.JsonIgnore]
    public string? UserId { get; init; }

    public AuditOperationType OperationType => AuditOperationType.Update;
    public object? GetTargetId() => BillOfMaterialId;
    public string? GetTargetDisplayName() => null;
    public string? GetActionDescription() => "Added BOM component item";
}
