namespace NOIR.Application.Features.Accounting.Commands.DeleteAccount;

/// <summary>
/// Command to delete an account (soft delete).
/// Fails if the account is a system account, has children, or is referenced by journal lines.
/// </summary>
public sealed record DeleteAccountCommand(
    Guid Id,
    string? AccountCode = null) : IAuditableCommand
{
    [System.Text.Json.Serialization.JsonIgnore]
    public string? UserId { get; init; }

    public AuditOperationType OperationType => AuditOperationType.Delete;
    public object? GetTargetId() => Id;
    public string? GetTargetDisplayName() => AccountCode ?? Id.ToString();
    public string? GetActionDescription() => $"Deleted account '{GetTargetDisplayName()}'";
}
