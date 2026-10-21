namespace NOIR.Application.Features.Estimation.Commands.SubmitBoq;

/// <summary>
/// Command to submit a BOQ for review.
/// </summary>
public sealed record SubmitBoqCommand(Guid Id) : IAuditableCommand<BoqDto>
{
    [System.Text.Json.Serialization.JsonIgnore]
    public string? UserId { get; init; }

    public AuditOperationType OperationType => AuditOperationType.Update;
    public object? GetTargetId() => Id;
    public string? GetTargetDisplayName() => Id.ToString();
    public string? GetActionDescription() => "Submitted BOQ for review";
}
