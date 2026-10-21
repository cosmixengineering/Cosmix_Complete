namespace NOIR.Application.Features.Hr.Commands.LinkEmployeeToUser;

public sealed record LinkEmployeeToUserCommand(
    Guid EmployeeId,
    string TargetUserId) : IAuditableCommand<Features.Hr.DTOs.EmployeeDto>
{
    [System.Text.Json.Serialization.JsonIgnore]
    public string? UserId { get; init; }

    public AuditOperationType OperationType => AuditOperationType.Update;
    public object? GetTargetId() => EmployeeId;
    public string? GetTargetDisplayName() => EmployeeId.ToString();
    public string? GetActionDescription() => $"Linked employee to user '{TargetUserId}'";
}
