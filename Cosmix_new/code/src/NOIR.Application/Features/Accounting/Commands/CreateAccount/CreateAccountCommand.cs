namespace NOIR.Application.Features.Accounting.Commands.CreateAccount;

/// <summary>
/// Command to create a new chart of accounts entry.
/// </summary>
public sealed record CreateAccountCommand(
    string Code,
    string Name,
    AccountType Type,
    Guid? ParentId,
    string? Description) : IAuditableCommand<AccountDto>
{
    [System.Text.Json.Serialization.JsonIgnore]
    public string? UserId { get; init; }

    public AuditOperationType OperationType => AuditOperationType.Create;
    public object? GetTargetId() => null;
    public string? GetTargetDisplayName() => $"{Code} - {Name}";
    public string? GetActionDescription() => $"Created account '{Code} - {Name}'";
}
