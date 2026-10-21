namespace NOIR.Domain.Entities.Accounting;

/// <summary>
/// Single debit or credit line within a journal entry.
/// Exactly one of Debit or Credit must be positive on every line.
/// </summary>
public class JournalLine : Entity<Guid>
{
    // Associations
    public Guid JournalEntryId { get; private set; }
    public Guid AccountId { get; private set; }

    // Amounts (exactly one is positive per line)
    public decimal Debit { get; private set; }
    public decimal Credit { get; private set; }

    // Memo
    public string? Memo { get; private set; }

    // Navigation
    public virtual JournalEntry JournalEntry { get; private set; } = null!;

    // Private constructor for EF Core
    private JournalLine() { }

    /// <summary>
    /// Factory method to create a new journal line.
    /// Validates that exactly one of debit/credit is positive.
    /// </summary>
    public static JournalLine Create(
        Guid journalEntryId,
        Guid accountId,
        decimal debit,
        decimal credit,
        string? memo)
    {
        ValidateAmounts(debit, credit);

        return new JournalLine
        {
            Id = Guid.NewGuid(),
            JournalEntryId = journalEntryId,
            AccountId = accountId,
            Debit = debit,
            Credit = credit,
            Memo = memo
        };
    }

    /// <summary>
    /// Updates the line's account, amounts, and memo.
    /// </summary>
    public void Update(Guid accountId, decimal debit, decimal credit, string? memo)
    {
        ValidateAmounts(debit, credit);

        AccountId = accountId;
        Debit = debit;
        Credit = credit;
        Memo = memo;
    }

    private static void ValidateAmounts(decimal debit, decimal credit)
    {
        if (debit < 0)
            throw new ArgumentException("Debit amount cannot be negative.", nameof(debit));

        if (credit < 0)
            throw new ArgumentException("Credit amount cannot be negative.", nameof(credit));

        if (debit > 0 && credit > 0)
            throw new ArgumentException("A journal line cannot have both debit and credit amounts.", nameof(credit));

        if (debit <= 0 && credit <= 0)
            throw new ArgumentException("A journal line must have either a debit or a credit amount.", nameof(debit));
    }
}
