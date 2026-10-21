namespace NOIR.Application.Features.Crm.Commands.CreateActivity;

public sealed record CreateActivityCommand(
    ActivityType Type,
    string Subject,
    Guid PerformedById,
    DateTimeOffset PerformedAt,
    string? Description = null,
    Guid? ContactId = null,
    Guid? LeadId = null,
    int? DurationMinutes = null) : IAuditableCommand<Features.Crm.DTOs.ActivityDto>
{
    [System.Text.Json.Serialization.JsonIgnore]
    public string? AuditUserId { get; init; }

    public AuditOperationType OperationType => AuditOperationType.Create;
    public object? GetTargetId() => AuditUserId;
    public string? GetTargetDisplayName() => Subject;
    public string? GetActionDescription() => $"Created CRM activity '{Subject}'";
}
