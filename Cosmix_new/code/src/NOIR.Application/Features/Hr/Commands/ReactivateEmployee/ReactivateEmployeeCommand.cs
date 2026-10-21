namespace NOIR.Application.Features.Hr.Commands.ReactivateEmployee;

public sealed record ReactivateEmployeeCommand(Guid Id) : IAuditableCommand<Features.Hr.DTOs.EmployeeDto>
{
    [System.Text.Json.Serialization.JsonIgnore]
    public string? UserId { get; init; }

    public AuditOperationType OperationType => AuditOperationType.Update;
    public object? GetTargetId() => Id;
    public string? GetTargetDisplayName() => Id.ToString();
    public string? GetActionDescription() => "Reactivated employee";
}
