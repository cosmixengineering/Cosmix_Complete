using Cosmix.Domain.Common;

namespace Cosmix.Domain.Attendance.Rosters;

public sealed class TemporaryShiftChange : IAppendOnlyEntity
{
    private TemporaryShiftChange() { }
    public TemporaryShiftChange(Guid siteId, DateOnly attendanceDate, Guid originalShiftVersionId, Guid replacementShiftVersionId,
        Guid approvalRequestId, string approvedBy, DateTimeOffset approvedAtUtc, string writtenEvidenceReference, string reason)
        : this(siteId, attendanceDate, originalShiftVersionId, replacementShiftVersionId,
            (Guid?)ModelGuard.Id(approvalRequestId, nameof(approvalRequestId)), approvedBy, approvedAtUtc,
            writtenEvidenceReference, reason) { }

    private TemporaryShiftChange(Guid siteId, DateOnly attendanceDate, Guid originalShiftVersionId, Guid replacementShiftVersionId,
        Guid? approvalRequestId, string recordedByUserId, DateTimeOffset recordedAt, string writtenEvidenceReference, string reason)
    {
        if (originalShiftVersionId == replacementShiftVersionId) throw new ArgumentException("A temporary change needs a different shift version.");
        Id = Guid.NewGuid(); SiteId = ModelGuard.Id(siteId, nameof(siteId)); AttendanceDate = attendanceDate;
        OriginalShiftVersionId = ModelGuard.Id(originalShiftVersionId, nameof(originalShiftVersionId)); ReplacementShiftVersionId = ModelGuard.Id(replacementShiftVersionId, nameof(replacementShiftVersionId));
        ApprovalRequestId = approvalRequestId; ApprovedBy = ModelGuard.Required(recordedByUserId, 200, nameof(recordedByUserId));
        ApprovedAtUtc = ModelGuard.Utc(recordedAt); WrittenEvidenceReference = ModelGuard.Required(writtenEvidenceReference, 500, nameof(writtenEvidenceReference));
        Reason = ModelGuard.Required(reason, 2000, nameof(reason));
    }

    /// <summary>
    /// Records a directly authorized site change without inventing an approval request. The
    /// application must capture written evidence and the immutable affected-employee set atomically.
    /// </summary>
    public static TemporaryShiftChange CreateDirect(Guid siteId, DateOnly attendanceDate,
        Guid originalShiftVersionId, Guid replacementShiftVersionId, string recordedByUserId,
        DateTimeOffset recordedAt, string writtenEvidenceReference, string reason) =>
        new(siteId, attendanceDate, originalShiftVersionId, replacementShiftVersionId, null,
            recordedByUserId, recordedAt, writtenEvidenceReference, reason);
    public Guid Id { get; private set; }
    public Guid SiteId { get; private set; }
    public DateOnly AttendanceDate { get; private set; }
    public Guid OriginalShiftVersionId { get; private set; }
    public Guid ReplacementShiftVersionId { get; private set; }
    /// <summary>Retained for legacy approved changes; null for explicitly authorized direct writes.</summary>
    public Guid? ApprovalRequestId { get; private set; }
    /// <summary>Legacy column name; for direct writes this is the recording actor, not a separate approver.</summary>
    public string ApprovedBy { get; private set; } = string.Empty;
    /// <summary>Legacy column name; for direct writes this is the recording timestamp.</summary>
    public DateTimeOffset ApprovedAtUtc { get; private set; }
    public string WrittenEvidenceReference { get; private set; } = string.Empty;
    public string Reason { get; private set; } = string.Empty;
    public IReadOnlyCollection<TemporaryShiftChangeEmployee> Employees => _employees;
    private readonly List<TemporaryShiftChangeEmployee> _employees = [];
}
