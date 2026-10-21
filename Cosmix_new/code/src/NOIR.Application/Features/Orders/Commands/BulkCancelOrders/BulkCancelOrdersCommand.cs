namespace NOIR.Application.Features.Orders.Commands.BulkCancelOrders;

/// <summary>
/// Command to cancel multiple orders in a single operation.
/// </summary>
public sealed record BulkCancelOrdersCommand(List<Guid> OrderIds, string? Reason = null) : IAuditableCommand
{
    [System.Text.Json.Serialization.JsonIgnore]
    public string? UserId { get; init; }

    public AuditOperationType OperationType => AuditOperationType.Update;
    public object? GetTargetId() => OrderIds.Count == 1 ? OrderIds[0] : string.Join(",", OrderIds.Take(5));
    public string? GetTargetDisplayName() => $"{OrderIds.Count} orders";
    public string? GetActionDescription() => $"Bulk cancelled {OrderIds.Count} orders";
}
