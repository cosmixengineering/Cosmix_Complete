namespace NOIR.Application.Features.Accounting.Commands.AddJournalLine;

/// <summary>
/// Command to add a line to a draft journal entry.
/// </summary>
public sealed record AddJournalLineCommand(
    Guid JournalEntryId,
    Guid AccountId,
    decimal Debit,
    decimal Credit,
    string? Memo) : IAuditableCommand
{
    [System.Text.Json.Serialization.JsonIgnore]
    public string? UserId { get; init; }

    public AuditOperationType OperationType => AuditOperationType.Update;
    public object? GetTargetId() => JournalEntryId;
    public string? GetTargetDisplayName() => JournalEntryId.ToString();
    public string? GetActionDescription() => $"Added a journal line to entry '{JournalEntryId}'";
}
