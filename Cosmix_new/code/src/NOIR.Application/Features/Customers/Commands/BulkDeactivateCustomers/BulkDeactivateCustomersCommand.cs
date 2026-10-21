namespace NOIR.Application.Features.Customers.Commands.BulkDeactivateCustomers;

/// <summary>
/// Command to deactivate multiple customers in a single operation.
/// </summary>
public sealed record BulkDeactivateCustomersCommand(List<Guid> CustomerIds) : IAuditableCommand
{
    [System.Text.Json.Serialization.JsonIgnore]
    public string? UserId { get; init; }

    public AuditOperationType OperationType => AuditOperationType.Update;
    public object? GetTargetId() => CustomerIds.Count == 1 ? CustomerIds[0] : string.Join(",", CustomerIds.Take(5));
    public string? GetTargetDisplayName() => $"{CustomerIds.Count} customers";
    public string? GetActionDescription() => $"Bulk deactivated {CustomerIds.Count} customers";
}
