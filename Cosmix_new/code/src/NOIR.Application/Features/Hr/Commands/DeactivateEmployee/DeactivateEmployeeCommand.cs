namespace NOIR.Application.Features.Hr.Commands.DeactivateEmployee;

public sealed record DeactivateEmployeeCommand(
    Guid Id,
    EmployeeStatus Status) : IAuditableCommand<Features.Hr.DTOs.EmployeeDto>
{
    [System.Text.Json.Serialization.JsonIgnore]
    public string? UserId { get; init; }

    public AuditOperationType OperationType => AuditOperationType.Update;
    public object? GetTargetId() => Id;
    public string? GetTargetDisplayName() => Id.ToString();
    public string? GetActionDescription() => $"Deactivated employee with status '{Status}'";
}
