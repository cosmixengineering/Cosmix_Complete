namespace NOIR.Application.Features.Estimation.Commands.DeleteBoq;

/// <summary>
/// Command to delete a BOQ (soft delete). Only draft BOQs can be deleted.
/// </summary>
public sealed record DeleteBoqCommand(
    Guid Id,
    string? BoqNumber = null) : IAuditableCommand
{
    [System.Text.Json.Serialization.JsonIgnore]
    public string? UserId { get; init; }

    public AuditOperationType OperationType => AuditOperationType.Delete;
    public object? GetTargetId() => Id;
    public string? GetTargetDisplayName() => BoqNumber ?? Id.ToString();
    public string? GetActionDescription() => $"Deleted BOQ '{GetTargetDisplayName()}'";
}
