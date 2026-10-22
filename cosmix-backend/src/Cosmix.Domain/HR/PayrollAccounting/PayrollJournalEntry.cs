using Cosmix.Domain.Common;

namespace Cosmix.Domain.HR.PayrollAccounting;

public sealed class PayrollJournalEntry
{
    private PayrollJournalEntry() { }

    public PayrollJournalEntry(Guid obligationId, JournalEntryKind kind, string debitAccountCode,
        string creditAccountCode, decimal amount, string currency, string description,
        string postedByUserId, DateTimeOffset postedAt)
    {
        Id = Guid.NewGuid();
        ObligationId = ModelGuard.Id(obligationId, nameof(obligationId));
        Kind = Enum.IsDefined(kind) ? kind : throw new ArgumentOutOfRangeException(nameof(kind));
        DebitAccountCode = ModelGuard.Required(debitAccountCode, 50, nameof(debitAccountCode));
        CreditAccountCode = ModelGuard.Required(creditAccountCode, 50, nameof(creditAccountCode));
        Amount = HrModelGuard.Money(amount, nameof(amount));
        Currency = HrModelGuard.Currency(currency);
        Description = ModelGuard.Required(description, 500, nameof(description));
        PostedByUserId = ModelGuard.Required(postedByUserId, 200, nameof(postedByUserId));
        PostedAt = ModelGuard.Utc(postedAt);
    }

    public Guid Id { get; private set; }
    public Guid ObligationId { get; private set; }
    public PayrollObligation Obligation { get; private set; } = null!;
    public JournalEntryKind Kind { get; private set; }
    public string DebitAccountCode { get; private set; } = string.Empty;
    public string CreditAccountCode { get; private set; } = string.Empty;
    public decimal Amount { get; private set; }
    public string Currency { get; private set; } = string.Empty;
    public string Description { get; private set; } = string.Empty;
    public DateTimeOffset PostedAt { get; private set; }
    public string PostedByUserId { get; private set; } = string.Empty;
}
