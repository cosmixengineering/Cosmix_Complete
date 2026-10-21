namespace NOIR.Domain.Entities.Accounting;

/// <summary>
/// Double-entry journal entry (aggregate root for JournalLine).
/// Enforces balanced debits/credits at creation and posting time.
/// </summary>
public class JournalEntry : TenantAggregateRoot<Guid>
{
    private readonly List<JournalLine> _lines = [];

    // Identity
    public string EntryNumber { get; private set; } = string.Empty;
    public DateOnly EntryDate { get; private set; }

    // Reference data
    public string? Reference { get; private set; }
    public string? Description { get; private set; }

    // Lifecycle
    public JournalEntryStatus Status { get; private set; } = JournalEntryStatus.Draft;

    // Source tracking (optional link back to originating document)
    public string? SourceType { get; private set; }
    public Guid? SourceId { get; private set; }

    // Posting audit
    public string? PostedByUserId { get; private set; }
    public DateTimeOffset? PostedAt { get; private set; }

    // Void audit
    public string? VoidedByUserId { get; private set; }
    public DateTimeOffset? VoidedAt { get; private set; }
    public string? VoidReason { get; private set; }

    // Navigation
    public IReadOnlyCollection<JournalLine> Lines => _lines.AsReadOnly();

    // Private constructor for EF Core
    private JournalEntry() : base() { }

    private JournalEntry(Guid id, string? tenantId) : base(id, tenantId) { }

    /// <summary>
    /// Factory method to create a new draft journal entry.
    /// </summary>
    public static JournalEntry Create(
        string entryNumber,
        DateOnly entryDate,
        string? reference,
        string? description,
        string? tenantId = null)
    {
        return new JournalEntry(Guid.NewGuid(), tenantId)
        {
            EntryNumber = entryNumber,
            EntryDate = entryDate,
            Reference = reference,
            Description = description,
            Status = JournalEntryStatus.Draft
        };
    }

    /// <summary>
    /// Adds a debit/credit line to the entry. Only allowed while Draft.
    /// </summary>
    public void AddLine(JournalLine line)
    {
        ArgumentNullException.ThrowIfNull(line);

        if (Status != JournalEntryStatus.Draft)
            throw new InvalidOperationException("Lines can only be modified while the journal entry is a draft.");

        _lines.Add(line);
    }

    /// <summary>
    /// Removes a line from the entry by ID. Only allowed while Draft.
    /// </summary>
    public void RemoveLine(Guid lineId)
    {
        if (Status != JournalEntryStatus.Draft)
            throw new InvalidOperationException("Lines can only be modified while the journal entry is a draft.");

        var line = _lines.FirstOrDefault(l => l.Id == lineId)
            ?? throw new InvalidOperationException($"Journal line '{lineId}' was not found on this entry.");

        _lines.Remove(line);
    }

    /// <summary>
    /// Updates the entry header fields. Only allowed while Draft.
    /// </summary>
    public void UpdateHeader(DateOnly entryDate, string? reference, string? description)
    {
        if (Status != JournalEntryStatus.Draft)
            throw new InvalidOperationException("Only draft journal entries can be modified.");

        EntryDate = entryDate;
        Reference = reference;
        Description = description;
    }

    /// <summary>
    /// Posts the entry, making it effective in the ledger.
    /// Requires at least two lines and total debits equal to total credits (within 0.01 tolerance).
    /// </summary>
    public void Post(string? postedByUserId)
    {
        if (Status != JournalEntryStatus.Draft)
            throw new InvalidOperationException("Only draft journal entries can be posted.");

        if (_lines.Count < 2)
            throw new InvalidOperationException("A journal entry must have at least two lines to post.");

        var totalDebit = _lines.Sum(l => l.Debit);
        var totalCredit = _lines.Sum(l => l.Credit);

        if (Math.Abs(totalDebit - totalCredit) > 0.01m)
            throw new InvalidOperationException(
                $"Journal entry is not balanced: total debits ({totalDebit:0.00}) must equal total credits ({totalCredit:0.00}).");

        Status = JournalEntryStatus.Posted;
        PostedByUserId = postedByUserId;
        PostedAt = DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// Voids a posted entry, recording who voided it and why.
    /// Voided entries are retained for audit purposes.
    /// </summary>
    public void Void(string? voidedByUserId, string? reason)
    {
        if (Status != JournalEntryStatus.Posted)
            throw new InvalidOperationException("Only posted journal entries can be voided.");

        Status = JournalEntryStatus.Void;
        VoidedByUserId = voidedByUserId;
        VoidedAt = DateTimeOffset.UtcNow;
        VoidReason = reason;
    }
}
