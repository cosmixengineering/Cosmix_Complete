namespace NOIR.Application.Features.Pm.Commands.RemoveProjectMember;

public sealed record RemoveProjectMemberCommand(
    Guid ProjectId,
    Guid MemberId) : IAuditableCommand<Features.Pm.DTOs.ProjectMemberDto>
{
    [System.Text.Json.Serialization.JsonIgnore]
    public string? AuditUserId { get; init; }

    public AuditOperationType OperationType => AuditOperationType.Delete;
    public object? GetTargetId() => MemberId;
    public string? GetTargetDisplayName() => null;
    public string? GetActionDescription() => "Removed member from project";
}
