namespace NOIR.Application.Features.Estimation.Commands.UpdateBoq;

/// <summary>
/// Command to update an existing BOQ header.
/// </summary>
public sealed record UpdateBoqCommand(
    Guid Id,
    string Title,
    string? Description,
    Guid? CustomerId,
    Guid? ProjectId,
    decimal MarkupPercent,
    string? Notes) : IAuditableCommand<BoqDto>
{
    [System.Text.Json.Serialization.JsonIgnore]
    public string? UserId { get; init; }

    public AuditOperationType OperationType => AuditOperationType.Update;
    public object? GetTargetId() => Id;
    public string? GetTargetDisplayName() => Title;
    public string? GetActionDescription() => $"Updated BOQ '{Title}'";
}
