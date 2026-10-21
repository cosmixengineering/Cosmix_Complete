namespace NOIR.Application.Features.Crm.Commands.UpdateActivity;

public sealed record UpdateActivityCommand(
    Guid Id,
    ActivityType Type,
    string Subject,
    DateTimeOffset PerformedAt,
    string? Description = null,
    Guid? ContactId = null,
    Guid? LeadId = null,
    int? DurationMinutes = null) : IAuditableCommand<Features.Crm.DTOs.ActivityDto>
{
    [System.Text.Json.Serialization.JsonIgnore]
    public string? AuditUserId { get; init; }

    public AuditOperationType OperationType => AuditOperationType.Update;
    public object? GetTargetId() => Id;
    public string? GetTargetDisplayName() => Subject;
    public string? GetActionDescription() => $"Updated CRM activity '{Subject}'";
}
