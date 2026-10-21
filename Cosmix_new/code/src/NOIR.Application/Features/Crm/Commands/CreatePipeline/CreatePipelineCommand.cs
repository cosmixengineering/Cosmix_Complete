namespace NOIR.Application.Features.Crm.Commands.CreatePipeline;

public sealed record CreatePipelineCommand(
    string Name,
    bool IsDefault,
    List<Features.Crm.DTOs.CreatePipelineStageDto> Stages) : IAuditableCommand<Features.Crm.DTOs.PipelineDto>
{
    [System.Text.Json.Serialization.JsonIgnore]
    public string? AuditUserId { get; init; }

    public AuditOperationType OperationType => AuditOperationType.Create;
    public object? GetTargetId() => AuditUserId;
    public string? GetTargetDisplayName() => Name;
    public string? GetActionDescription() => $"Created pipeline '{Name}'";
}
