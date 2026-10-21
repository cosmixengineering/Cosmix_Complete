namespace NOIR.Application.Features.Crm.Commands.DeleteActivity;

public sealed record DeleteActivityCommand(Guid Id) : IAuditableCommand<Features.Crm.DTOs.ActivityDto>
{
    [System.Text.Json.Serialization.JsonIgnore]
    public string? AuditUserId { get; init; }

    public AuditOperationType OperationType => AuditOperationType.Delete;
    public object? GetTargetId() => Id;
    public string? GetTargetDisplayName() => "CRM Activity";
    public string? GetActionDescription() => "Deleted CRM activity";
}
