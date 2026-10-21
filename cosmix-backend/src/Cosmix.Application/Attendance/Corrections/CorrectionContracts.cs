using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Cosmix.Application.Authentication.Access;
using Cosmix.Application.Attendance.Common;
using Cosmix.Application.Common.Models;
using Cosmix.Application.Attendance.Rosters;
using Cosmix.Application.Attendance.Offices;
using Cosmix.Domain.Attendance.Corrections;
using Cosmix.Domain.Attendance.DailySummaries;
using Cosmix.Domain.Attendance.Punches;
using Cosmix.Domain.Attendance.WorkSessions;
using Cosmix.Domain.Common.Approvals;
using Cosmix.Domain.Attendance.FaceVerification;

namespace Cosmix.Application.Attendance.Corrections;

public static class CorrectionPermissions
{
    public const string Self = "attendance.corrections.self";
    public const string ExceptionsSelf = "attendance.exceptions.self";
    public const string Submit = "attendance.corrections.submit";
    public const string Review = "attendance.corrections.review";
    public const string Decide = "attendance.corrections.decide";
    public const string AlternativesDecide = "attendance.corrections.alternatives.decide";
    public const string AlternativesConfigure = "attendance.corrections.alternatives.configure";
    public static IReadOnlyList<AccessPermissionDefinition> Definitions { get; } = Array.AsReadOnly<AccessPermissionDefinition>([
        new(Self, "Submit/read own immutable correction proposals and cancel own pending proposals", false, false, true),
        new(ExceptionsSelf, "Read own attendance exception notices without confidential review reasons", false, false, true),
        new(Submit, "Submit and manage own requests for scoped employees", true, true, false),
        new(Review, "Read scoped HR correction queue and protected evidence", true, true, false),
        new(Decide, "Decide scoped HR corrections, including documented missing or failed Check-In duty", true, true, false),
        new(AlternativesDecide, "Fixed Admin approval of documented medical/accessibility/out-of-boundary duty, with explicitly assigned scope", true, true, false),
        new(AlternativesConfigure, "Fixed Admin configuration of versioned alternative-duty evidence requirements", true, true, false)
    ]);
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record CreateCorrectionRequest(DateOnly BusinessDate, AttendanceCorrectionKind Kind,
    Guid? RawPunchId, [property: JsonRequired] Guid? ExpectedAssessmentId, [property: JsonRequired] Guid? ExpectedSummaryId,
    DateTimeOffset? RequestedTimeUtc, DateTimeOffset? RequestedEndUtc, AttendanceDayStatus? RequestedStatus,
    [Required, StringLength(1000)] string Reason, [Required, StringLength(4000)] string WrittenEvidence,
    // Required for a NEW proposal; omission is accepted only to replay an existing legacy receipt.
    // Omit-on-null preserves the exact legacy request digest and protected snapshot JSON contract.
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? ExpectedSourceFingerprint = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] Guid? TargetWorkSessionId = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] Guid? ResubmissionOfCorrectionId = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] CorrectionAlternativeKind? AlternativeKind = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] Guid? ExpectedAlternativePolicyId = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<Guid>? AlternativeEvidenceDocumentIds = null);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record CancelCorrectionRequest([Required, StringLength(12, MinimumLength = 12)] string ExpectedVersion,
    [Required, StringLength(1000)] string Reason);

public sealed record CorrectionListRequest(DateOnly From, DateOnly To, [Range(1, 1000000)] int Page = 1,
    [Range(1, 100)] int PageSize = 25);
public sealed record CorrectionSourceResponse(DateOnly BusinessDate, Guid? RawPunchId, string? Action,
    DateTimeOffset? OriginalTimeUtc, Guid? LatestAssessmentId, string? AssessmentOutcome,
    Guid? LatestSummaryId, int? SummaryRevision, string? SummaryStatus, bool PeriodLocked,
    string RoutingStatus = "routing_not_configured", string SummaryCalculationState = "not_calculated",
    EffectiveRosterResponse? HistoricalRoster = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? SourceFingerprint = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<CorrectionSessionSource>? SessionRevisions = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] CorrectionPunchContext? OriginalPunchContext = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<CorrectionDutyProof>? DutyProofs = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] OfficeAttendanceSource? OfficeSource = null);
public sealed record CorrectionDutyProof(Guid WorkSessionId, Guid? CheckInPunchId, Guid? AssessmentId,
    string? AssessmentOutcome, Guid? VerificationId, bool HistoricallyVerified);
public sealed record CorrectionCheckInState(Guid WorkSessionId, RawPunchEvent? Punch,
    PunchAssessment? Assessment, FaceVerificationAttempt? Verification);
public sealed record CorrectionPunchContext(DateOnly? BusinessDate, Guid? SiteId, Guid? EmployeeAssignmentId,
    Guid? ShiftVersionId, Guid? GeofenceVersionId, Guid? SiteRosterAssignmentId, Guid? TemporaryShiftChangeId,
    Guid? HolidayCalendarId, bool IsOffline,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] Guid? BranchId = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] Guid? OfficeRosterAssignmentId = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] Guid? OfficeGeofenceVersionId = null);
public sealed record CorrectionSessionSource(Guid Id, Guid SessionKey, int Revision, DateTimeOffset StartsAtUtc,
    DateTimeOffset? EndsAtUtc, Guid? CheckInPunchId, Guid? CheckOutPunchId, Guid? CorrectionId,
    Guid? SiteId, Guid ShiftVersionId, Guid? PolicyVersionId, bool EndsContinuousDuty,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] Guid? BranchId = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] Guid? OfficeRosterAssignmentId = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] Guid? EmployeeAssignmentId = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] Guid? OfficeGeofenceVersionId = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] Guid? HolidayCalendarId = null);
public sealed record CorrectionResponse(Guid Id, Guid EmployeeId, DateOnly BusinessDate, string Kind,
    Guid? RawPunchId, Guid? OriginalSummaryId, DateTimeOffset? OriginalTimeUtc, DateTimeOffset? RequestedTimeUtc,
    DateTimeOffset? RequestedEndUtc, string? RequestedStatus, string Reason, DateTimeOffset SubmittedAtUtc,
    string State, string RoutingStatus, string Version, bool ChangesAttendance = false,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] Guid? AppliedSessionRevisionId = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] Guid? AppliedSummaryRevisionId = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Interpretation = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] CorrectionAlternativeKind? AlternativeKind = null);
public sealed record CorrectionDetailResponse(CorrectionResponse Request, string WrittenEvidence,
    CorrectionSourceResponse CapturedSource,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] CorrectionAlternativeEvidence? AlternativeEvidence = null);
public sealed record AttendanceExceptionNotice(Guid Id, DateOnly BusinessDate, string NoticeCode,
    DateTimeOffset DetectedAtUtc, Guid? PunchId, Guid? SessionRevisionId, Guid? SummaryId,
    string NextAction = "review_own_evidence_or_submit_correction");

public sealed record CorrectionSourceState(RawPunchEvent? Punch, PunchAssessment? Assessment,
    DailyAttendanceSummary? Summary, bool PeriodLocked, IReadOnlyList<WorkSession> SessionRevisions,
    IReadOnlyList<CorrectionCheckInState>? CheckInSources = null);
public sealed record CorrectionRecord
{
    public AttendanceCorrection Correction { get; init; } = null!;
    public ApprovalRequest Approval { get; init; } = null!;
    public Guid? AppliedSessionRevisionId { get; init; }
    public Guid? AppliedSummaryRevisionId { get; init; }
    public CorrectionRecord() { }
    public CorrectionRecord(AttendanceCorrection correction, ApprovalRequest approval)
    { Correction = correction; Approval = approval; }
}
public sealed record CorrectionEvidenceSnapshot(int SchemaVersion, Guid SubmissionId, Guid EmployeeId, Guid RequesterAccountId,
    CreateCorrectionRequest Proposal, CorrectionSourceResponse Source,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<AttendanceResourceScope>? OriginalResourceScopes = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] CorrectionAlternativeEvidence? AlternativeEvidence = null);

public interface IAttendanceCorrectionRepository
{
    Task<DateOnly?> JoinedOnAsync(Guid employeeId, CancellationToken ct);
    Task<CorrectionSourceState> SourceAsync(Guid employeeId, DateOnly date, Guid? punchId, CancellationToken ct);
    Task<bool> HasPendingEquivalentAsync(Guid employeeId, CreateCorrectionRequest request, CancellationToken ct);
    Task<CorrectionRecord?> FindOwnAsync(Guid employeeId, Guid requesterAccountId, Guid id, CancellationToken ct);
    Task<PagedResult<CorrectionRecord>> ListOwnAsync(Guid employeeId, Guid requesterAccountId, CorrectionListRequest request, CancellationToken ct);
    Task<PagedResult<AttendanceException>> ExceptionsAsync(Guid employeeId, CorrectionListRequest request, CancellationToken ct);
    void Add(ApprovalRequest request, AttendanceCorrection correction);
    void ExpectVersion(ApprovalRequest request, byte[] version);
    Task SaveChangesAsync(CancellationToken ct);
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record DecideCorrectionRequest([Required, StringLength(12, MinimumLength = 12)] string ExpectedVersion,
    ApprovalState Decision, [Required, StringLength(1000)] string Reason,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] CorrectionAlternativeKind? AlternativeKind = null);
public sealed record CorrectionDecisionResponse(CorrectionResponse Request, Guid DecisionId,
    Guid? AppliedSessionRevisionId, Guid? AppliedSummaryRevisionId);
public enum CorrectionQueueScope { All = 1, Employee = 2, Site = 3, Department = 4, Team = 5, Project = 6 }
public sealed record CorrectionQueueRequest(DateOnly From, DateOnly To, CorrectionQueueScope Scope,
    Guid? TargetId = null, ApprovalState State = ApprovalState.Pending, int PageSize = 25, string? Cursor = null);
public sealed record CorrectionQueueResponse(IReadOnlyList<CorrectionResponse> Items, string? NextCursor);
public interface ICorrectionReviewRepository
{
    Task<CorrectionRecord?> FindAsync(Guid employeeId, Guid id, CancellationToken ct);
    Task<IReadOnlyList<CorrectionRecord>> CandidatesAsync(CorrectionQueueRequest request, AttendanceCursorPosition? after, int limit, CancellationToken ct, Guid? requesterId = null);
    Task<bool> ScopeExistsAsync(CorrectionQueueScope scope, Guid id, CancellationToken ct);
    Task<RawPunchEvent?> PunchAsync(Guid employeeId, Guid punchId, CancellationToken ct);
    Task<bool> OverlapsAsync(Guid employeeId, Guid excludedSessionKey, DateTimeOffset start, DateTimeOffset end, CancellationToken ct);
    Task<bool> PreservesAssignmentAsync(Guid employeeId, Guid? assignmentId, Guid? siteId, DateTimeOffset start, DateTimeOffset end, CancellationToken ct);
    Task<bool> HistoricallyEligibleAsync(Guid employeeId, DateTimeOffset start, DateTimeOffset end, CancellationToken ct);
    void Add(ApprovalDecision decision, WorkSession? session);
}
