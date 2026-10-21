namespace NOIR.Application.Features.Accounting.Commands.CreateJournalEntry;

/// <summary>
/// Command to create a new draft journal entry with balanced lines.
/// </summary>
public sealed record CreateJournalEntryCommand(
    DateOnly EntryDate,
    string? Reference,
    string? Description,
    List<CreateJournalLineRequest> Lines) : IAuditableCommand<JournalEntryDto>
{
    [System.Text.Json.Serialization.JsonIgnore]
    public string? UserId { get; init; }

    public AuditOperationType OperationType => AuditOperationType.Create;
    public object? GetTargetId() => null;
    public string? GetTargetDisplayName() => EntryDate.ToString("yyyy-MM-dd");
    public string? GetActionDescription() => $"Created journal entry dated '{EntryDate:yyyy-MM-dd}'";
}
