namespace NOIR.Application.Features.Crm.Commands.DeletePipeline;

public sealed record DeletePipelineCommand(Guid Id) : IAuditableCommand<Features.Crm.DTOs.PipelineDto>
{
    [System.Text.Json.Serialization.JsonIgnore]
    public string? AuditUserId { get; init; }

    public AuditOperationType OperationType => AuditOperationType.Delete;
    public object? GetTargetId() => Id;
    public string? GetTargetDisplayName() => "Pipeline";
    public string? GetActionDescription() => "Deleted pipeline";
}
