namespace NOIR.Application.Features.Pm.Commands.AddProjectMember;

public sealed record AddProjectMemberCommand(
    Guid ProjectId,
    Guid EmployeeId,
    ProjectMemberRole Role) : IAuditableCommand<Features.Pm.DTOs.ProjectMemberDto>
{
    [System.Text.Json.Serialization.JsonIgnore]
    public string? AuditUserId { get; init; }

    public AuditOperationType OperationType => AuditOperationType.Create;
    public object? GetTargetId() => ProjectId;
    public string? GetTargetDisplayName() => null;
    public string? GetActionDescription() => "Added member to project";
}
