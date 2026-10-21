namespace NOIR.Application.Features.Accounting.Commands.VoidJournalEntry;

/// <summary>
/// Command to void a posted journal entry, recording the reason.
/// </summary>
public sealed record VoidJournalEntryCommand(
    Guid Id,
    string? Reason) : IAuditableCommand
{
    [System.Text.Json.Serialization.JsonIgnore]
    public string? UserId { get; init; }

    public AuditOperationType OperationType => AuditOperationType.Update;
    public object? GetTargetId() => Id;
    public string? GetTargetDisplayName() => Id.ToString();
    public string? GetActionDescription() => $"Voided journal entry '{Id}'";
}
