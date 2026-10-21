namespace NOIR.Application.Features.Estimation.Commands.ApproveBoq;

/// <summary>
/// Command to approve a BOQ under review. Creates a revision snapshot.
/// </summary>
public sealed record ApproveBoqCommand(Guid Id) : IAuditableCommand<BoqDto>
{
    [System.Text.Json.Serialization.JsonIgnore]
    public string? UserId { get; init; }

    public AuditOperationType OperationType => AuditOperationType.Update;
    public object? GetTargetId() => Id;
    public string? GetTargetDisplayName() => Id.ToString();
    public string? GetActionDescription() => "Approved BOQ";
}
