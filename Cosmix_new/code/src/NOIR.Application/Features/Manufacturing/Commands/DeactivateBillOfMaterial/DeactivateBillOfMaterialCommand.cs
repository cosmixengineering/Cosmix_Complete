namespace NOIR.Application.Features.Manufacturing.Commands.DeactivateBillOfMaterial;

/// <summary>
/// Command to deactivate a bill of material.
/// </summary>
public sealed record DeactivateBillOfMaterialCommand(Guid Id) : IAuditableCommand<BillOfMaterialDto>
{
    [System.Text.Json.Serialization.JsonIgnore]
    public string? UserId { get; init; }

    public AuditOperationType OperationType => AuditOperationType.Update;
    public object? GetTargetId() => Id;
    public string? GetTargetDisplayName() => null;
    public string? GetActionDescription() => "Deactivated bill of material";
}
