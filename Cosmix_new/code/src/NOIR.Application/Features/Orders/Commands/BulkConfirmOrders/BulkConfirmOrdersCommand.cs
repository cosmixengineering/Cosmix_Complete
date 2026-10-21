namespace NOIR.Application.Features.Orders.Commands.BulkConfirmOrders;

/// <summary>
/// Command to confirm multiple orders in a single operation.
/// </summary>
public sealed record BulkConfirmOrdersCommand(List<Guid> OrderIds) : IAuditableCommand
{
    [System.Text.Json.Serialization.JsonIgnore]
    public string? UserId { get; init; }

    public AuditOperationType OperationType => AuditOperationType.Update;
    public object? GetTargetId() => OrderIds.Count == 1 ? OrderIds[0] : string.Join(",", OrderIds.Take(5));
    public string? GetTargetDisplayName() => $"{OrderIds.Count} orders";
    public string? GetActionDescription() => $"Bulk confirmed {OrderIds.Count} orders";
}
