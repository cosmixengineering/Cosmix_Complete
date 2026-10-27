using Cosmix.Domain.Common;

namespace Cosmix.Domain.HR.PayrollAccounting;

public sealed class PayrollJournalEntry : IAppendOnlyEntity
{
    private PayrollJournalEntry() { }

    public PayrollJournalEntry(Guid obligationId, JournalEntryKind kind, string debitAccountCode,
        string creditAccountCode, decimal amount, string currency, string description,
        string postedByUserId, DateTimeOffset postedAt,
        Guid? statutoryContributionId = null, string? scheme = null)
    {
        Id = Guid.NewGuid();
        ObligationId = ModelGuard.Id(obligationId, nameof(obligationId));
        Kind = Enum.IsDefined(kind) ? kind : throw new ArgumentOutOfRangeException(nameof(kind));
        DebitAccountCode = ModelGuard.Required(debitAccountCode, 50, nameof(debitAccountCode));
        CreditAccountCode = ModelGuard.Required(creditAccountCode, 50, nameof(creditAccountCode));
        Amount = HrModelGuard.Money(amount, nameof(amount));
        if (Amount <= 0 || string.Equals(DebitAccountCode, CreditAccountCode, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Journal amounts must be positive and accounts different.");
        Currency = HrModelGuard.Currency(currency);
        Description = ModelGuard.Required(description, 500, nameof(description));
        PostedByUserId = ModelGuard.Required(postedByUserId, 200, nameof(postedByUserId));
        PostedAt = ModelGuard.Utc(postedAt);
        StatutoryContributionId = statutoryContributionId.HasValue ? ModelGuard.Id(statutoryContributionId.Value, nameof(statutoryContributionId)) : null;
        Scheme = ModelGuard.Optional(scheme, 50, nameof(scheme));
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
    public Guid? StatutoryContributionId { get; private set; }
    public string? Scheme { get; private set; }
    public DateOnly? PostingDate { get; private set; }
    public Guid? AccountingPeriodId { get; private set; }
    public Guid? PostingProfileId { get; private set; }
    public string? SourceKey { get; private set; }
    public void BindPosting(DateOnly date, Guid periodId, Guid? profileId, string sourceKey)
    {
        if (SourceKey is not null) throw new InvalidOperationException("Posting source is immutable.");
        PostingDate = date; AccountingPeriodId = ModelGuard.Id(periodId, nameof(periodId));
        PostingProfileId = profileId.HasValue ? ModelGuard.Id(profileId.Value, nameof(profileId)) : null;
        SourceKey = ModelGuard.Required(sourceKey, 180, nameof(sourceKey));
    }
}
