namespace NOIR.Application.Features.Manufacturing.Commands.CreateBillOfMaterial;

/// <summary>
/// Command to create a new bill of material with its component items.
/// </summary>
public sealed record CreateBillOfMaterialCommand(
    string Code,
    string Name,
    Guid ProductId,
    decimal OutputQuantity,
    string Unit,
    string? Description,
    List<CreateBillOfMaterialItemRequest> Items) : IAuditableCommand<BillOfMaterialDto>
{
    [System.Text.Json.Serialization.JsonIgnore]
    public string? UserId { get; init; }

    public AuditOperationType OperationType => AuditOperationType.Create;
    public object? GetTargetId() => null;
    public string? GetTargetDisplayName() => Code;
    public string? GetActionDescription() => $"Created bill of material '{Code}'";
}
