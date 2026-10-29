using Cosmix.Domain.Common;

namespace Cosmix.Domain.HR.StatutoryContributions;

/// <summary>Immutable coverage of one approved contribution entry by one frozen payroll obligation.</summary>
public sealed class StatutoryEmployeePosting : IAppendOnlyEntity
{
    private StatutoryEmployeePosting() { }

    public StatutoryEmployeePosting(Guid entryId, Guid contributionId, Guid obligationId, Guid? journalEntryId,
        string sourceFingerprint, string payableAccountCode, string actor, DateTimeOffset at)
    {
        EntryId = ModelGuard.Id(entryId, nameof(entryId));
        ContributionId = ModelGuard.Id(contributionId, nameof(contributionId));
        ObligationId = ModelGuard.Id(obligationId, nameof(obligationId));
        JournalEntryId = journalEntryId.HasValue ? ModelGuard.Id(journalEntryId.Value, nameof(journalEntryId)) : null;
        SourceFingerprint = ModelGuard.Required(sourceFingerprint, 64, nameof(sourceFingerprint));
        if (SourceFingerprint.Length != 64 || SourceFingerprint.Any(x => !char.IsAsciiHexDigit(x)))
            throw new ArgumentException("Frozen salary fingerprint is required.");
        PostedBy = ModelGuard.Required(actor, 200, nameof(actor));
        PayableAccountCode = ModelGuard.Required(payableAccountCode, 50, nameof(payableAccountCode));
        PostedAt = ModelGuard.Utc(at);
    }

    public Guid EntryId { get; private set; }
    public Guid ContributionId { get; private set; }
    public Guid ObligationId { get; private set; }
    public Guid? JournalEntryId { get; private set; }
    public string SourceFingerprint { get; private set; } = string.Empty;
    public string PostedBy { get; private set; } = string.Empty;
    public string PayableAccountCode { get; private set; } = string.Empty;
    public DateTimeOffset PostedAt { get; private set; }
}
