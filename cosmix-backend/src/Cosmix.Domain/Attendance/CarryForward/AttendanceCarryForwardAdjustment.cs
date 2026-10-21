using Cosmix.Domain.Attendance.Corrections;
using Cosmix.Domain.Attendance.DailySummaries;
using Cosmix.Domain.Attendance.MonthLocks;
using Cosmix.Domain.Attendance.WorkSessions;
using Cosmix.Domain.Common;
using Cosmix.Domain.Common.Approvals;

namespace Cosmix.Domain.Attendance.CarryForward;

public sealed record AttendanceCarryForwardWindow(Guid SessionKey, DateTimeOffset StartsAtUtc, DateTimeOffset EndsAtUtc);

/// <summary>Approved factual difference posted to another calendar month. Never a new raw punch,
/// target-date duty session, payable amount, or rewrite of the locked source interpretation.</summary>
public sealed class AttendanceCarryForwardAdjustment : IAppendOnlyEntity
{
    public const string RequestType = "attendance.locked-correction";
    private AttendanceCarryForwardAdjustment() { }
    public AttendanceCarryForwardAdjustment(AttendanceCorrection correction, ApprovalRequest request, ApprovalDecision decision,
        AttendancePeriodLock sourcePeriod, AttendancePeriodLockEvent sourceEvent, AttendancePeriodSummary membership,
        DailyAttendanceSummary sourceSummary, IReadOnlyList<WorkSession> frozenSessions, AttendanceCarryForwardAdjustment? prior,
        DateOnly targetMonthStart, IReadOnlyList<AttendanceCarryForwardWindow> interpretedSessions,
        string inputFingerprint, string interpretationFingerprint, string protectedEvidenceReference, DateTimeOffset appliedAtUtc)
    {
        if (!sourcePeriod.IsLocked || !sourceEvent.IsLocked || sourceEvent.AttendancePeriodLockId != sourcePeriod.Id ||
            sourceEvent.Revision != sourcePeriod.Revision || membership.AttendancePeriodLockEventId != sourceEvent.Id ||
            membership.DailyAttendanceSummaryId != sourceSummary.Id || membership.EmployeeId != sourceSummary.EmployeeId ||
            membership.AttendanceDate != sourceSummary.AttendanceDate || sourceSummary.AttendanceDate < sourcePeriod.PeriodStart ||
            sourceSummary.AttendanceDate >= sourcePeriod.PeriodEndExclusive)
            throw new ArgumentException("An exact retained lock-event summary membership is required.");
        if (correction.EmployeeId != sourceSummary.EmployeeId || correction.AttendanceDate != sourceSummary.AttendanceDate ||
            correction.OriginalSummaryId != sourceSummary.Id || correction.AdjustmentPeriodLockId.HasValue ||
            correction.Kind == AttendanceCorrectionKind.DayStatus || request.RequestType != RequestType ||
            correction.ApprovalRequestId != request.Id || request.State != ApprovalState.Approved ||
            request.SnapshotPrivateReference != correction.PrivateEvidenceReference || correction.RequestedBy != request.RequesterUserId ||
            decision.ApprovalRequestId != request.Id || decision.Decision != ApprovalState.Approved ||
            decision.ActorUserId == request.RequesterUserId || decision.DecidedAt != request.ClosedAt ||
            decision.ProtectedChangeReference != correction.PrivateEvidenceReference || appliedAtUtc != decision.DecidedAt)
            throw new ArgumentException("The exact distinct approved correction decision must authorize this entry.");
        if (sourceEvent.AtUtc > correction.RequestedAtUtc || correction.RequestedAtUtc > appliedAtUtc ||
            prior is not null && prior.AppliedAtUtc > correction.RequestedAtUtc)
            throw new ArgumentException("Submission must follow the frozen lock and previous adjustment, and precede its decision.");
        if (targetMonthStart == default || targetMonthStart.Day != 1 || targetMonthStart.Year == 9999 && targetMonthStart.Month == 12 ||
            targetMonthStart <= new DateOnly(sourceSummary.AttendanceDate.Year, sourceSummary.AttendanceDate.Month, 1))
            throw new ArgumentException("A complete calendar month after the source month is required.");
        if (frozenSessions.Any(x => x.EmployeeId != sourceSummary.EmployeeId || x.AttendanceDate != sourceSummary.AttendanceDate || x.EndsAtUtc is null))
            throw new ArgumentException("Frozen session membership must match this employee and business date.");
        var baseline = Ticks(frozenSessions.Select(x => new AttendanceCarryForwardWindow(x.SessionKey, x.StartsAtUtc, x.EndsAtUtc!.Value)).ToArray(), true);
        if (baseline / TimeSpan.TicksPerMinute != sourceSummary.WorkedMinutes)
            throw new ArgumentException("Frozen session totals must match the pinned summary.");
        if (prior is not null && (prior.EmployeeId != sourceSummary.EmployeeId || prior.SourceBusinessDate != sourceSummary.AttendanceDate ||
            prior.SourcePeriodLockEventId != sourceEvent.Id || prior.SourceSummaryId != sourceSummary.Id))
            throw new ArgumentException("The previous adjustment must share the same immutable source.");
        var after = Ticks(interpretedSessions, false);
        Id = Guid.NewGuid(); EmployeeId = sourceSummary.EmployeeId; SourceBusinessDate = sourceSummary.AttendanceDate;
        SourcePeriodLockEventId = sourceEvent.Id; SourceSummaryId = sourceSummary.Id; CorrectionId = correction.Id;
        ApprovalDecisionId = decision.Id; Revision = checked((prior?.Revision ?? 0) + 1); SupersedesAdjustmentId = prior?.Id;
        TargetMonthStart = targetMonthStart; TargetMonthEndExclusive = targetMonthStart.AddMonths(1);
        BeforeWorkedTicks = prior?.AfterWorkedTicks ?? baseline; AfterWorkedTicks = after; DeltaWorkedTicks = checked(after - BeforeWorkedTicks);
        FirstStartsAtUtc = interpretedSessions.Min(x => x.StartsAtUtc).ToUniversalTime(); LastEndsAtUtc = interpretedSessions.Max(x => x.EndsAtUtc).ToUniversalTime();
        InputFingerprint = Hash(inputFingerprint); InterpretationFingerprint = Hash(interpretationFingerprint);
        ProtectedEvidenceReference = ModelGuard.Required(protectedEvidenceReference, 500, nameof(protectedEvidenceReference));
        AppliedAtUtc = ModelGuard.Utc(appliedAtUtc);
    }
    private static long Ticks(IReadOnlyList<AttendanceCarryForwardWindow> sessions, bool emptyAllowed)
    {
        if (sessions.Count > 4096 || !emptyAllowed && sessions.Count == 0 ||
            sessions.Select(x => x.SessionKey).Distinct().Count() != sessions.Count) throw new ArgumentException("Bounded unique session windows are required.");
        long ticks = 0; DateTimeOffset? previousEnd = null;
        foreach (var session in sessions.OrderBy(x => x.StartsAtUtc).ThenBy(x => x.SessionKey))
        {
            if (session.SessionKey == Guid.Empty || session.EndsAtUtc <= session.StartsAtUtc || previousEnd > session.StartsAtUtc)
                throw new ArgumentException("Session windows must be ordered, closed and nonoverlapping.");
            ticks = checked(ticks + (session.EndsAtUtc - session.StartsAtUtc).Ticks); previousEnd = session.EndsAtUtc;
        }
        if (ticks / TimeSpan.TicksPerMinute > int.MaxValue) throw new ArgumentException("The factual duration exceeds supported summary storage.");
        return ticks;
    }
    private static string Hash(string value)
    {
        value = ModelGuard.Required(value, 64, nameof(value)).ToUpperInvariant();
        return value.Length == 64 && value.All(Uri.IsHexDigit) ? value : throw new ArgumentException("A SHA-256 fingerprint is required.");
    }
    public Guid Id { get; private set; }
    public Guid EmployeeId { get; private set; }
    public DateOnly SourceBusinessDate { get; private set; }
    public Guid SourcePeriodLockEventId { get; private set; }
    public Guid SourceSummaryId { get; private set; }
    public Guid CorrectionId { get; private set; }
    public Guid ApprovalDecisionId { get; private set; }
    public int Revision { get; private set; }
    public Guid? SupersedesAdjustmentId { get; private set; }
    public DateOnly TargetMonthStart { get; private set; }
    public DateOnly TargetMonthEndExclusive { get; private set; }
    public long BeforeWorkedTicks { get; private set; }
    public long AfterWorkedTicks { get; private set; }
    public long DeltaWorkedTicks { get; private set; }
    public DateTimeOffset FirstStartsAtUtc { get; private set; }
    public DateTimeOffset LastEndsAtUtc { get; private set; }
    public string InputFingerprint { get; private set; } = "";
    public string InterpretationFingerprint { get; private set; } = "";
    public string ProtectedEvidenceReference { get; private set; } = "";
    public DateTimeOffset AppliedAtUtc { get; private set; }
}
