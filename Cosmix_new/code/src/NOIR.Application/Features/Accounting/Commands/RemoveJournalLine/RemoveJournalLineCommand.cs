namespace NOIR.Application.Features.Accounting.Commands.RemoveJournalLine;

/// <summary>
/// Command to remove a line from a draft journal entry.
/// </summary>
public sealed record RemoveJournalLineCommand(
    Guid JournalEntryId,
    Guid LineId) : IAuditableCommand
{
    [System.Text.Json.Serialization.JsonIgnore]
    public string? UserId { get; init; }

    public AuditOperationType OperationType => AuditOperationType.Update;
    public object? GetTargetId() => JournalEntryId;
    public string? GetTargetDisplayName() => JournalEntryId.ToString();
    public string? GetActionDescription() => $"Removed a journal line from entry '{JournalEntryId}'";
}
