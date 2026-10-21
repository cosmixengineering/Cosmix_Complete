using Cosmix.Domain.Common;

namespace Cosmix.Domain.Attendance.Rosters;

/// <summary>An immutable direct cancellation or supersession; the original change and manifest stay intact.</summary>
public sealed class TemporaryShiftChangeAmendment : IAppendOnlyEntity
{
    private TemporaryShiftChangeAmendment() { }

    public TemporaryShiftChangeAmendment(TemporaryShiftChange original, TemporaryShiftChange? replacement,
        string recordedBy, DateTimeOffset recordedAtUtc, string writtenEvidenceReference, string reason)
    {
        ArgumentNullException.ThrowIfNull(original);
        if (original.ApprovalRequestId.HasValue || replacement?.ApprovalRequestId.HasValue == true)
            throw new ArgumentException("Legacy approved changes require a separate historical review.");
        if (replacement is not null && (replacement.Id == original.Id || replacement.SiteId != original.SiteId ||
            replacement.AttendanceDate != original.AttendanceDate))
            throw new ArgumentException("A successor must be a different change for the same site and business date.");
        Id = Guid.NewGuid();
        OriginalChangeId = ModelGuard.Id(original.Id, nameof(original));
        ReplacementChangeId = replacement?.Id;
        RecordedBy = ModelGuard.Required(recordedBy, 200, nameof(recordedBy));
        RecordedAtUtc = ModelGuard.Utc(recordedAtUtc);
        WrittenEvidenceReference = ModelGuard.Required(writtenEvidenceReference, 500, nameof(writtenEvidenceReference));
        Reason = ModelGuard.Required(reason, 2000, nameof(reason));
    }

    public Guid Id { get; private set; }
    public Guid OriginalChangeId { get; private set; }
    public Guid? ReplacementChangeId { get; private set; }
    public string RecordedBy { get; private set; } = string.Empty;
    public DateTimeOffset RecordedAtUtc { get; private set; }
    public string WrittenEvidenceReference { get; private set; } = string.Empty;
    public string Reason { get; private set; } = string.Empty;
}
