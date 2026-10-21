namespace NOIR.Application.Features.Crm.Commands.UpdatePipeline;

public sealed record UpdatePipelineCommand(
    Guid Id,
    string Name,
    bool IsDefault,
    List<Features.Crm.DTOs.UpdatePipelineStageDto> Stages) : IAuditableCommand<Features.Crm.DTOs.PipelineDto>
{
    [System.Text.Json.Serialization.JsonIgnore]
    public string? AuditUserId { get; init; }

    public AuditOperationType OperationType => AuditOperationType.Update;
    public object? GetTargetId() => Id;
    public string? GetTargetDisplayName() => Name;
    public string? GetActionDescription() => $"Updated pipeline '{Name}'";
}
