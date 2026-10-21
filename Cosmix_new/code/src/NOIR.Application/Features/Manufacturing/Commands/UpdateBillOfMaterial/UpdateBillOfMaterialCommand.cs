namespace NOIR.Application.Features.Manufacturing.Commands.UpdateBillOfMaterial;

/// <summary>
/// Command to update a bill of material header.
/// </summary>
public sealed record UpdateBillOfMaterialCommand(
    Guid Id,
    string Name,
    Guid ProductId,
    decimal OutputQuantity,
    string Unit,
    string? Description) : IAuditableCommand<BillOfMaterialDto>
{
    [System.Text.Json.Serialization.JsonIgnore]
    public string? UserId { get; init; }

    public AuditOperationType OperationType => AuditOperationType.Update;
    public object? GetTargetId() => Id;
    public string? GetTargetDisplayName() => null;
    public string? GetActionDescription() => "Updated bill of material";
}
