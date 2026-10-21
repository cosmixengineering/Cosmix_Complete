namespace NOIR.Application.Features.Pm.Commands.UpdateProject;

public sealed record UpdateProjectCommand(
    Guid Id,
    string Name,
    string? Description = null,
    ProjectStatus? Status = null,
    DateTimeOffset? StartDate = null,
    DateTimeOffset? EndDate = null,
    DateTimeOffset? DueDate = null,
    decimal? Budget = null,
    string? Currency = null,
    string? Color = null,
    string? Icon = null,
    ProjectVisibility? Visibility = null) : IAuditableCommand<Features.Pm.DTOs.ProjectDto>
{
    [System.Text.Json.Serialization.JsonIgnore]
    public string? AuditUserId { get; init; }

    public AuditOperationType OperationType => AuditOperationType.Update;
    public object? GetTargetId() => Id;
    public string? GetTargetDisplayName() => Name;
    public string? GetActionDescription() => $"Updated project '{Name}'";
}
