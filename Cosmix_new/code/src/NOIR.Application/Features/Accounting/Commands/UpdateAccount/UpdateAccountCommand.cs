namespace NOIR.Application.Features.Accounting.Commands.UpdateAccount;

/// <summary>
/// Command to update an existing account.
/// </summary>
public sealed record UpdateAccountCommand(
    Guid Id,
    string Code,
    string Name,
    Guid? ParentId,
    string? Description) : IAuditableCommand<AccountDto>
{
    [System.Text.Json.Serialization.JsonIgnore]
    public string? UserId { get; init; }

    public AuditOperationType OperationType => AuditOperationType.Update;
    public object? GetTargetId() => Id;
    public string? GetTargetDisplayName() => $"{Code} - {Name}";
    public string? GetActionDescription() => $"Updated account '{Code} - {Name}'";
}
