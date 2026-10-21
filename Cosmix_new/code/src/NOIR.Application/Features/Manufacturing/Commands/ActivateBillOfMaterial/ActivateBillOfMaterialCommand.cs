namespace NOIR.Application.Features.Manufacturing.Commands.ActivateBillOfMaterial;

/// <summary>
/// Command to activate a bill of material.
/// </summary>
public sealed record ActivateBillOfMaterialCommand(Guid Id) : IAuditableCommand<BillOfMaterialDto>
{
    [System.Text.Json.Serialization.JsonIgnore]
    public string? UserId { get; init; }

    public AuditOperationType OperationType => AuditOperationType.Update;
    public object? GetTargetId() => Id;
    public string? GetTargetDisplayName() => null;
    public string? GetActionDescription() => "Activated bill of material";
}
