namespace NOIR.Application.Features.Hr.Commands.CreateEmployee;

public sealed record CreateEmployeeCommand(
    string FirstName,
    string LastName,
    string Email,
    Guid DepartmentId,
    DateTimeOffset JoinDate,
    EmploymentType EmploymentType,
    string? Phone = null,
    string? AvatarUrl = null,
    string? Position = null,
    Guid? ManagerId = null,
    string? UserId = null,
    string? Notes = null) : IAuditableCommand<Features.Hr.DTOs.EmployeeDto>
{
    [System.Text.Json.Serialization.JsonIgnore]
    public string? AuditUserId { get; init; }

    public AuditOperationType OperationType => AuditOperationType.Create;
    public object? GetTargetId() => AuditUserId;
    public string? GetTargetDisplayName() => $"{FirstName} {LastName}";
    public string? GetActionDescription() => $"Created employee '{FirstName} {LastName}'";
}
