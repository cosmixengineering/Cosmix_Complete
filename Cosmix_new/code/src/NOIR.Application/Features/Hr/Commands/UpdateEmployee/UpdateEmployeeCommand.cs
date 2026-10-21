namespace NOIR.Application.Features.Hr.Commands.UpdateEmployee;

public sealed record UpdateEmployeeCommand(
    Guid Id,
    string FirstName,
    string LastName,
    string Email,
    Guid DepartmentId,
    EmploymentType EmploymentType,
    string? Phone = null,
    string? AvatarUrl = null,
    string? Position = null,
    Guid? ManagerId = null,
    string? Notes = null) : IAuditableCommand<Features.Hr.DTOs.EmployeeDto>
{
    [System.Text.Json.Serialization.JsonIgnore]
    public string? UserId { get; init; }

    public AuditOperationType OperationType => AuditOperationType.Update;
    public object? GetTargetId() => Id;
    public string? GetTargetDisplayName() => $"{FirstName} {LastName}";
    public string? GetActionDescription() => $"Updated employee '{FirstName} {LastName}'";
}
