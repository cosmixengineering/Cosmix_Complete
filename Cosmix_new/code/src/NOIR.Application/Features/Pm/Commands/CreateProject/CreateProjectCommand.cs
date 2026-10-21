namespace NOIR.Application.Features.Pm.Commands.CreateProject;

public sealed record CreateProjectCommand(
    string Name,
    string? Description = null,
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

    public AuditOperationType OperationType => AuditOperationType.Create;
    public object? GetTargetId() => AuditUserId;
    public string? GetTargetDisplayName() => Name;
    public string? GetActionDescription() => $"Created project '{Name}'";
}
