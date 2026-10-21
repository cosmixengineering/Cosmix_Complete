namespace NOIR.Application.Features.Pm.Commands.ArchiveProject;

public sealed record ArchiveProjectCommand(Guid Id) : IAuditableCommand<Features.Pm.DTOs.ProjectDto>
{
    [System.Text.Json.Serialization.JsonIgnore]
    public string? AuditUserId { get; init; }

    public AuditOperationType OperationType => AuditOperationType.Update;
    public object? GetTargetId() => Id;
    public string? GetTargetDisplayName() => null;
    public string? GetActionDescription() => "Archived project";
}
