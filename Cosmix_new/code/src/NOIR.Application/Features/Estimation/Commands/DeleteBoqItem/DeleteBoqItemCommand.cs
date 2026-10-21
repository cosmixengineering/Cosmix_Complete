namespace NOIR.Application.Features.Estimation.Commands.DeleteBoqItem;

/// <summary>
/// Command to remove a line item from a BOQ.
/// </summary>
public sealed record DeleteBoqItemCommand(
    Guid BoqId,
    Guid ItemId) : IAuditableCommand
{
    [System.Text.Json.Serialization.JsonIgnore]
    public string? UserId { get; init; }

    public AuditOperationType OperationType => AuditOperationType.Delete;
    public object? GetTargetId() => ItemId;
    public string? GetTargetDisplayName() => ItemId.ToString();
    public string? GetActionDescription() => $"Removed BOQ item '{ItemId}'";
}
