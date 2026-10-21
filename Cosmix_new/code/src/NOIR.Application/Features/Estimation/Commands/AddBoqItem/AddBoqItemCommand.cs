namespace NOIR.Application.Features.Estimation.Commands.AddBoqItem;

/// <summary>
/// Command to add a line item to a BOQ.
/// </summary>
public sealed record AddBoqItemCommand(
    Guid BoqId,
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

    public AuditOperationType OperationType => AuditOperationType.Create;
    public object? GetTargetId() => BoqId;
    public string? GetTargetDisplayName() => ItemCode;
    public string? GetActionDescription() => $"Added BOQ item '{ItemCode}'";
}
