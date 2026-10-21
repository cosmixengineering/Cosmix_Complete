namespace NOIR.Application.Features.Estimation.Commands.UpdateBoqItem;

/// <summary>
/// Command to update a BOQ line item.
/// </summary>
public sealed record UpdateBoqItemCommand(
    Guid BoqId,
    Guid ItemId,
    string Section,
    string ItemCode,
    string Description,
    decimal Quantity,
    string Unit,
    decimal UnitRate,
    int SortOrder) : IAuditableCommand<BoqItemDto>
{
    [System.Text.Json.Serialization.JsonIgnore]
    public string? UserId { get; init; }

    public AuditOperationType OperationType => AuditOperationType.Update;
    public object? GetTargetId() => ItemId;
    public string? GetTargetDisplayName() => ItemCode;
    public string? GetActionDescription() => $"Updated BOQ item '{ItemCode}'";
}
