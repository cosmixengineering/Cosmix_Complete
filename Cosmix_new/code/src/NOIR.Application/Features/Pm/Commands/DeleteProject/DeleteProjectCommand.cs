namespace NOIR.Application.Features.Pm.Commands.DeleteProject;

public sealed record DeleteProjectCommand(Guid Id) : IAuditableCommand<Features.Pm.DTOs.ProjectDto>
{
    [System.Text.Json.Serialization.JsonIgnore]
    public string? AuditUserId { get; init; }

    public AuditOperationType OperationType => AuditOperationType.Delete;
    public object? GetTargetId() => Id;
    public string? GetTargetDisplayName() => null;
    public string? GetActionDescription() => "Deleted project";
}
