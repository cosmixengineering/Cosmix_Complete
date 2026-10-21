namespace NOIR.Application.Features.Customers.Commands.BulkDeleteCustomers;

/// <summary>
/// Command to soft-delete multiple customers in a single operation.
/// </summary>
public sealed record BulkDeleteCustomersCommand(List<Guid> CustomerIds) : IAuditableCommand
{
    [System.Text.Json.Serialization.JsonIgnore]
    public string? UserId { get; init; }

    public AuditOperationType OperationType => AuditOperationType.Delete;
    public object? GetTargetId() => CustomerIds.Count == 1 ? CustomerIds[0] : string.Join(",", CustomerIds.Take(5));
    public string? GetTargetDisplayName() => $"{CustomerIds.Count} customers";
    public string? GetActionDescription() => $"Bulk deleted {CustomerIds.Count} customers";
}
