namespace NOIR.Application.Features.Pm.Commands.ChangeProjectMemberRole;

public sealed record ChangeProjectMemberRoleCommand(
    Guid ProjectId,
    Guid MemberId,
    ProjectMemberRole Role) : IAuditableCommand<Features.Pm.DTOs.ProjectMemberDto>
{
    [System.Text.Json.Serialization.JsonIgnore]
    public string? AuditUserId { get; init; }

    public AuditOperationType OperationType => AuditOperationType.Update;
    public object? GetTargetId() => MemberId;
    public string? GetTargetDisplayName() => null;
    public string? GetActionDescription() => $"Changed member role to '{Role}'";
}
