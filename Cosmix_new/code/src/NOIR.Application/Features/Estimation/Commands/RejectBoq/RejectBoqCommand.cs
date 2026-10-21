namespace NOIR.Application.Features.Estimation.Commands.RejectBoq;

/// <summary>
/// Command to reject a BOQ under review.
/// </summary>
public sealed record RejectBoqCommand(
    Guid Id,
    string? Reason) : IAuditableCommand<BoqDto>
{
    [System.Text.Json.Serialization.JsonIgnore]
    public string? UserId { get; init; }

    public AuditOperationType OperationType => AuditOperationType.Update;
    public object? GetTargetId() => Id;
    public string? GetTargetDisplayName() => Id.ToString();
    public string? GetActionDescription() => "Rejected BOQ";
}
