namespace NOIR.Application.Features.Crm.Commands.MoveLeadStage;

public sealed record MoveLeadStageCommand(
    Guid LeadId,
    Guid NewStageId,
    double NewSortOrder) : IAuditableCommand<Features.Crm.DTOs.LeadDto>
{
    [System.Text.Json.Serialization.JsonIgnore]
    public string? AuditUserId { get; init; }

    public AuditOperationType OperationType => AuditOperationType.Update;
    public object? GetTargetId() => LeadId;
    public string? GetTargetDisplayName() => "Lead";
    public string? GetActionDescription() => "Moved lead to a different stage";
}
