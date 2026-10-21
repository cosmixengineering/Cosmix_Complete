namespace NOIR.Application.Features.Estimation.Commands.CreateBoq;

/// <summary>
/// Command to create a new BOQ.
/// </summary>
public sealed record CreateBoqCommand(
    string BoqNumber,
    string Title,
    Guid? CustomerId,
    Guid? ProjectId,
    string Currency,
    string? Description,
    string? Notes,
    decimal MarkupPercent) : IAuditableCommand<BoqDto>
{
    [System.Text.Json.Serialization.JsonIgnore]
    public string? UserId { get; init; }

    public AuditOperationType OperationType => AuditOperationType.Create;
    public object? GetTargetId() => null;
    public string? GetTargetDisplayName() => Title;
    public string? GetActionDescription() => $"Created BOQ '{BoqNumber}'";
}
