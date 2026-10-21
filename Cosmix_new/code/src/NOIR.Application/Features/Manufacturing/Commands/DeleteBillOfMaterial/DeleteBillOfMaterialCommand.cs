namespace NOIR.Application.Features.Manufacturing.Commands.DeleteBillOfMaterial;

/// <summary>
/// Command to delete a bill of material (soft delete).
/// Only inactive BOMs without production orders can be deleted.
/// </summary>
public sealed record DeleteBillOfMaterialCommand(
    Guid Id,
    string? BomCode = null) : IAuditableCommand
{
    [System.Text.Json.Serialization.JsonIgnore]
    public string? UserId { get; init; }

    public AuditOperationType OperationType => AuditOperationType.Delete;
    public object? GetTargetId() => Id;
    public string? GetTargetDisplayName() => BomCode ?? Id.ToString();
    public string? GetActionDescription() => $"Deleted bill of material '{GetTargetDisplayName()}'";
}
