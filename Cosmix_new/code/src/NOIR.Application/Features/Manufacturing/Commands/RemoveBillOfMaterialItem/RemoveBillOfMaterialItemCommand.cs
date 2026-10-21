namespace NOIR.Application.Features.Manufacturing.Commands.RemoveBillOfMaterialItem;

/// <summary>
/// Command to remove a component item from a bill of material.
/// </summary>
public sealed record RemoveBillOfMaterialItemCommand(
    Guid BillOfMaterialId,
    Guid ItemId) : IAuditableCommand<BillOfMaterialDto>
{
    [System.Text.Json.Serialization.JsonIgnore]
    public string? UserId { get; init; }

    public AuditOperationType OperationType => AuditOperationType.Update;
    public object? GetTargetId() => BillOfMaterialId;
    public string? GetTargetDisplayName() => null;
    public string? GetActionDescription() => "Removed BOM component item";
}
