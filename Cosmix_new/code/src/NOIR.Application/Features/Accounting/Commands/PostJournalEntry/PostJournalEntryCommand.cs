namespace NOIR.Application.Features.Accounting.Commands.PostJournalEntry;

/// <summary>
/// Command to post a draft journal entry, locking it and making it effective in the ledger.
/// </summary>
public sealed record PostJournalEntryCommand(
    Guid Id) : IAuditableCommand
{
    [System.Text.Json.Serialization.JsonIgnore]
    public string? UserId { get; init; }

    public AuditOperationType OperationType => AuditOperationType.Update;
    public object? GetTargetId() => Id;
    public string? GetTargetDisplayName() => Id.ToString();
    public string? GetActionDescription() => $"Posted journal entry '{Id}'";
}
