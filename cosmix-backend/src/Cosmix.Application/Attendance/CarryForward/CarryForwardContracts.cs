using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Cosmix.Application.Attendance.Common;
using Cosmix.Application.Attendance.Corrections;
using Cosmix.Application.Attendance.MonthLocks;
using Cosmix.Application.Attendance.Rosters;
using Cosmix.Application.Attendance.Offices;
using Cosmix.Domain.Attendance.CarryForward;
using Cosmix.Domain.Attendance.Corrections;
using Cosmix.Domain.Attendance.DailySummaries;
using Cosmix.Domain.Attendance.MonthLocks;
using Cosmix.Domain.Attendance.Punches;
using Cosmix.Domain.Attendance.WorkSessions;
using Cosmix.Domain.Common.Approvals;

namespace Cosmix.Application.Attendance.CarryForward;

public sealed record LockedPeriodRange(Guid Id, DateOnly Start, DateOnly EndExclusive);
public sealed record CarryForwardTarget(DateOnly Start, DateOnly EndExclusive);
public sealed record CarryForwardSession(Guid SessionKey, Guid? SourceWorkSessionId, DateTimeOffset StartsAtUtc,
    DateTimeOffset EndsAtUtc, Guid? SiteId, Guid ShiftVersionId, bool IsManual, Guid? OriginCorrectionId,
    Guid? CheckInPunchId, Guid? CheckOutPunchId,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] OfficeAttendanceSource? OfficeSource = null);
public sealed record CarryForwardSourceResponse(Guid EmployeeId, DateOnly SourceBusinessDate, Guid SourceLockEventId,
    Guid SourceSummaryId, int SourceSummaryRevision, Guid? PriorAdjustmentId, int PriorRevision,
    DateOnly TargetMonthStart, DateOnly TargetMonthEndExclusive, long CurrentWorkedTicks, int CurrentWorkedMinutes,
    string SourceFingerprint, IReadOnlyList<CarryForwardSession> Sessions, EffectiveRosterResponse HistoricalRoster,
    Guid? RawPunchId, Guid? LatestAssessmentId, string FinancialState = "pending_policy",
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] OfficeAttendanceSource? OfficeSource = null);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record SubmitCarryForwardRequest(DateOnly SourceBusinessDate, AttendanceCorrectionKind Kind,
    Guid? TargetSessionKey, Guid? RawPunchId, DateTimeOffset RequestedTimeUtc, DateTimeOffset? RequestedEndUtc,
    Guid ExpectedSourceLockEventId, Guid ExpectedSourceSummaryId, [property: JsonRequired] Guid? ExpectedPriorAdjustmentId,
    DateOnly ExpectedTargetMonthStart, [Required, StringLength(64, MinimumLength = 64)] string ExpectedSourceFingerprint,
    [Required, StringLength(1000)] string Reason, [Required, StringLength(4000)] string WrittenEvidence,
    Guid? ResubmissionOfRequestId = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] CorrectionAlternativeKind? AlternativeKind = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] Guid? ExpectedAlternativePolicyId = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<Guid>? AlternativeEvidenceDocumentIds = null);
public sealed record CarryForwardRequestResponse(Guid Id, Guid EmployeeId, DateOnly SourceBusinessDate, string Kind,
    string State, string Version, DateTimeOffset SubmittedAtUtc, DateOnly TargetMonthStart, DateOnly TargetMonthEndExclusive,
    Guid SourceLockEventId, Guid SourceSummaryId, Guid? PriorAdjustmentId, Guid? AppliedAdjustmentId,
    string RoutingStatus, string FinancialState = "pending_policy",
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] CorrectionAlternativeKind? AlternativeKind = null);
public sealed record CarryForwardDetailResponse(CarryForwardRequestResponse Request, string WrittenEvidence,
    CarryForwardSourceResponse Source, IReadOnlyList<CarryForwardSession> ProposedInterpretation,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] CorrectionAlternativeEvidence? AlternativeEvidence = null);
public sealed record CarryForwardRequestPage(IReadOnlyList<CarryForwardRequestResponse> Items, string? NextCursor);
public sealed record CarryForwardHistoryRequest(DateOnly From, DateOnly To, ApprovalState? State = null,
    int PageSize = 25, string? Cursor = null);
public sealed record CarryForwardDecisionResponse(CarryForwardRequestResponse Request, Guid DecisionId,
    Guid? AppliedAdjustmentId, long? DeltaWorkedTicks, int? DeltaWorkedMinutes, string FinancialState = "pending_policy");
public sealed record CarryForwardMonthRequest(DateOnly Month, int Page = 1, int PageSize = 25);
public sealed record CarryForwardEntryResponse(Guid Id, DateOnly SourceBusinessDate, int Revision, Guid CorrectionId,
    Guid SourceLockEventId, Guid SourceSummaryId, DateOnly TargetMonthStart, long DeltaWorkedTicks,
    int DeltaWorkedMinutes, DateTimeOffset AppliedAtUtc, string FinancialState = "pending_policy");
public sealed record CarryForwardMonthResponse(Guid EmployeeId, DateOnly MonthStart, DateOnly MonthEndExclusive,
    IReadOnlyList<CarryForwardEntryResponse> Entries, int TotalCount, int Page, int PageSize, long TotalDeltaWorkedTicks,
    long TotalDeltaWorkedMinutes, string Interpretation = "posted_adjustments_only", string FinancialState = "pending_policy");

public interface ICarryForwardDutyOverlap
{
    /// <summary>Caller owns a live authorized transaction. Replaces frozen original windows with validated
    /// latest virtual interpretations; never counts both. No authorization is conferred by this helper.</summary>
    Task<bool> HasOverlapAsync(Guid employeeId, IReadOnlyCollection<Guid> excludedSessionKeys,
        DateTimeOffset start, DateTimeOffset end, CancellationToken ct);
}
public sealed record CarryForwardFrozenRows(MonthLockExisting Lock, AttendancePeriodSummary Membership,
    DailyAttendanceSummary Summary, IReadOnlyList<AttendanceSummarySession> Links, IReadOnlyList<WorkSession> Sessions);
public sealed record CarryForwardRawEvidence(RawPunchEvent Punch, PunchAssessment? Assessment);
public sealed record CarryForwardRequestRow
{
    public AttendanceCorrection Correction { get; init; } = null!;
    public ApprovalRequest Approval { get; init; } = null!;
    public AttendanceCarryForwardAdjustment? Adjustment { get; init; }
}
public sealed record CarryForwardProposalSnapshot(int SchemaVersion, Guid SubmissionId, Guid EmployeeId,
    Guid RequesterAccountId, SubmitCarryForwardRequest Proposal, CarryForwardSourceResponse Source,
    IReadOnlyList<CarryForwardSession> ProposedInterpretation, IReadOnlyList<AttendanceResourceScope> OriginalScopes,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] CorrectionAlternativeEvidence? AlternativeEvidence = null);
public sealed record CarryForwardInterpretationSnapshot(int SchemaVersion, Guid EmployeeId, DateOnly SourceBusinessDate,
    Guid SourceLockEventId, Guid SourceSummaryId, Guid CorrectionId, Guid ApprovalDecisionId, int Revision,
    Guid? PriorAdjustmentId, DateOnly TargetMonthStart, DateOnly TargetMonthEndExclusive, long BeforeWorkedTicks,
    long AfterWorkedTicks, string InputFingerprint, string InterpretationFingerprint, DateTimeOffset AppliedAtUtc,
    IReadOnlyList<Guid> FrozenSessionIds, IReadOnlyList<Guid> FrozenSessionKeys,
    IReadOnlyList<CarryForwardSession> Sessions, IReadOnlyList<AttendanceResourceScope> OriginalScopes,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] CorrectionAlternativeEvidence? AlternativeEvidence = null);

public interface ICarryForwardRepository
{
    Task<CarryForwardFrozenRows> FrozenAsync(Guid employeeId, DateOnly date, CancellationToken ct);
    Task<IReadOnlyList<LockedPeriodRange>> LockedRangesAsync(DateOnly after, CancellationToken ct);
    Task<AttendanceCarryForwardAdjustment?> LatestAsync(Guid employeeId, DateOnly date, CancellationToken ct);
    Task<AttendanceCarryForwardAdjustment?> AdjustmentAsync(Guid id, CancellationToken ct);
    Task<ApprovalDecision?> DecisionAsync(Guid id, CancellationToken ct);
    Task<CarryForwardRawEvidence?> RawAsync(Guid employeeId, Guid id, CancellationToken ct);
    Task<CarryForwardRequestRow?> RequestAsync(Guid employeeId, Guid id, CancellationToken ct);
    Task<IReadOnlyList<CarryForwardRequestRow>> CandidatesAsync(CorrectionQueueRequest request,
        AttendanceCursorPosition? after, int limit, Guid? requester, CancellationToken ct, bool allStates = false);
    Task<bool> EquivalentPendingAsync(Guid employeeId, SubmitCarryForwardRequest request, CancellationToken ct);
    Task<IReadOnlyList<AttendanceCarryForwardAdjustment>> MonthAsync(Guid? employeeId, DateOnly start, DateOnly end, CancellationToken ct);
    Task<IReadOnlyList<WorkSession>> OverlappingSessionsAsync(Guid employeeId, IReadOnlyCollection<Guid> excludedSessionKeys,
        DateTimeOffset start, DateTimeOffset end, CancellationToken ct);
    Task<IReadOnlyList<AttendanceCarryForwardAdjustment>> OverlapAdjustmentsAsync(Guid employeeId,
        IReadOnlyCollection<DateOnly> rawDates, DateTimeOffset start, DateTimeOffset end, CancellationToken ct);
    void Add(AttendanceCorrection correction, ApprovalRequest request);
    void Add(ApprovalDecision decision, AttendanceCarryForwardAdjustment? adjustment);
    void ExpectVersion(ApprovalRequest request, byte[] version);
    Task SaveChangesAsync(CancellationToken ct);
}
