namespace NOIR.Application.Features.Manufacturing.Commands.ReportProductionProgress;

/// <summary>
/// Command to report production progress (increments added to running totals).
/// </summary>
public sealed record ReportProductionProgressCommand(
    Guid Id,
    decimal ProducedQuantity,
    decimal ScrapQuantity) : IAuditableCommand<ProductionOrderDto>
{
    [System.Text.Json.Serialization.JsonIgnore]
    public string? UserId { get; init; }

    public AuditOperationType OperationType => AuditOperationType.Update;
    public object? GetTargetId() => Id;
    public string? GetTargetDisplayName() => null;
    public string? GetActionDescription() =>
        $"Reported production progress (+{ProducedQuantity} produced, +{ScrapQuantity} scrap)";
}
