using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Cosmix.Application.Attendance.Common;
using Cosmix.Application.Attendance.Corrections;
using Cosmix.Application.Attendance.Rosters;
using Cosmix.Application.Authentication.Access;
using Cosmix.Domain.Attendance.Overtime;
using Cosmix.Domain.Common.Approvals;

namespace Cosmix.Application.Attendance.Overtime;

public static class OvertimePermissions
{
    public const string Self = "attendance.overtime.self";
    public const string Submit = "attendance.overtime.submit";
    public const string Review = "attendance.overtime.review";
    public const string Decide = "attendance.overtime.decide";
    public const string FinancialRead = "attendance.overtime.financial.read";
    public const string FinancialSelf = "attendance.overtime.financial.self";
    public const string EligibilityRead = "attendance.overtime.eligibility.read";
    public const string EligibilityManage = "attendance.overtime.eligibility.manage";
    public static IReadOnlyList<AccessPermissionDefinition> Definitions { get; } = [
        new(Self, "Read own overtime and submit/cancel own requests", false, false, true),
        new(Submit, "Submit explicit employee overtime within historical scope", false, true, false),
        new(Review, "Review scoped overtime evidence and history", true, true, false),
        new(Decide, "Make a separate HR overtime decision within scope", true, true, false),
        new(FinancialRead, "Read protected overtime amounts within employee scope", true, true, false),
        new(FinancialSelf, "Read own authorized overtime amounts", false, false, true),
        new(EligibilityRead, "Read global/scoped dated overtime eligibility history", false, true, false),
        new(EligibilityManage, "Configure dated site OT eligibility or employee inheritance as fixed Admin/SuperAdmin with an explicit matching global/scoped permission", true, true, false)
    ];
}
public enum OvertimeActivity { Work = 1, CompanyTravel = 2 }
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record SubmitOvertimeRequest(DateOnly BusinessDate, Guid WorkSessionId,
    DateTimeOffset StartsAtUtc, DateTimeOffset EndsAtUtc, OvertimeActivity Activity,
    [Required, StringLength(64, MinimumLength = 64)] string ExpectedSourceFingerprint,
    [Required, StringLength(1000)] string Reason, [Required, StringLength(4000)] string WrittenEvidence,
    [StringLength(200)] string? JobReference = null, Guid? ResubmissionOfRequestId = null);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record DecideOvertimeRequest([Required, StringLength(12, MinimumLength = 12)] string ExpectedVersion,
    ApprovalState Decision, [Required, StringLength(1000)] string Reason);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record CancelOvertimeRequest([Required, StringLength(12, MinimumLength = 12)] string ExpectedVersion,
    [Required, StringLength(1000)] string Reason);
public sealed record OvertimeHistoryRequest(DateOnly From, DateOnly To, ApprovalState? State = null, int PageSize = 25, string? Cursor = null);
public sealed record OvertimeQueueRequest(DateOnly From, DateOnly To, CorrectionQueueScope Scope,
    Guid? TargetId = null, ApprovalState? State = ApprovalState.Pending, int PageSize = 25, string? Cursor = null);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record OvertimeBulkItem(Guid EmployeeId, SubmitOvertimeRequest Request);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record SubmitOvertimeBulkRequest([Required] IReadOnlyList<OvertimeBulkItem> Employees,
    [Required, StringLength(1000)] string Reason);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record OvertimeBulkDecisionItem(Guid EmployeeId, Guid RequestId, DecideOvertimeRequest Decision);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record DecideOvertimeBulkRequest([Required] IReadOnlyList<OvertimeBulkDecisionItem> Employees,
    [Required, StringLength(1000)] string Reason);

public sealed record OvertimeEligibility(string Status, Guid? VersionId, bool? Allowed,
    Guid? GlobalVersionId = null, Guid? EmployeeVersionId = null);
public sealed record OvertimeEligibleInterval(DateTimeOffset StartsAtUtc, DateTimeOffset EndsAtUtc);
public sealed record OvertimeDurationResult(string Status, Guid? PolicyVersionId, string? CalculationVersion,
    bool? ScheduledDutyCompleted, IReadOnlyList<OvertimeEligibleInterval> EligibleIntervals, string? RateRuleCode = null);
public sealed record OvertimeSessionSource(Guid Id, Guid SessionKey, int Revision, DateTimeOffset StartsAtUtc,
    DateTimeOffset? EndsAtUtc, Guid? SiteId, Guid ShiftVersionId, Guid? PolicyVersionId,
    Guid? CheckInAssessmentId, Guid? CheckOutAssessmentId, Guid? CorrectionDecisionId, bool ValidDutyEvidence,
    Guid? CorrectionId = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] Guid? BranchId = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] Guid? OfficeRosterAssignmentId = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] Guid? EmployeeAssignmentId = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] Guid? OfficeGeofenceVersionId = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] Guid? HolidayCalendarId = null);
public sealed record OvertimeSourceResponse(Guid EmployeeId, DateOnly BusinessDate, string StaffCategory,
    Guid? SiteId, EffectiveRosterResponse Roster, IReadOnlyList<OvertimeSessionSource> Sessions,
    Guid? SummaryId, string? SummaryInputFingerprint, bool IsLocked, OvertimeEligibility Eligibility,
    OvertimeDurationResult DurationPolicy, string SourceFingerprint);
public sealed record OvertimeResponse(Guid Id, Guid EmployeeId, DateOnly BusinessDate, Guid WorkSessionId,
    DateTimeOffset StartsAtUtc, DateTimeOffset EndsAtUtc, int RequestedMinutes, int? ApprovedMinutes,
    Guid? SiteId, string? JobReference, string State, string Version, DateTimeOffset RequestedAtUtc,
    Guid? BulkSubmissionId, Guid? DecisionId, string DurationState, string FinancialState = "pending_rate_policy",
    decimal? PayableAmount = null, long? ApprovedTicks = null, string? Currency = null);
public sealed record OvertimeDetail(OvertimeResponse Request, OvertimeSourceResponse Source,
    string? WrittenEvidence, string EvidenceVisibility);
public sealed record OvertimeFinancialResponse(Guid RequestId, Guid EmployeeId, DateOnly BusinessDate, string State,
    long? ApprovedTicks, decimal? Amount, string? Currency, string? FormulaVersion,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] OvertimeFinancialAward? Award = null);

/// <summary>Restricted Finance projection of immutable approved evidence. Never HTTP input,
/// a settlement instruction, or proof of posting. Obtain it through the authorized financial read.</summary>
public sealed record OvertimeFinancialAward(int SchemaVersion, Guid RequestId, Guid ApprovalRequestId,
    Guid DecisionId, Guid SubmissionId, Guid EmployeeId, DateOnly SourceBusinessDate, Guid WorkSessionId,
    Guid SiteId, string? JobReference, OvertimeSourceSnapshot Inputs,
    IReadOnlyList<OvertimeEligibleInterval> ApprovedIntervals, OvertimeFinancialEvidence FinancialEvidence,
    OvertimeAwardAuthorization Authorization, string AwardFingerprint);
public sealed record OvertimeAwardAuthorization(Guid RequesterAccountId, Guid ReviewerAccountId,
    string ReviewerRoleCode, DateTimeOffset ApprovedAtUtc);
public sealed record OvertimePage(IReadOnlyList<OvertimeResponse> Items, string? NextCursor);
public sealed record OvertimeBulkResponse(Guid BulkId, IReadOnlyList<OvertimeResponse> Employees);

// Protected source contracts are never accepted from HTTP and expose no compensation amount.
public sealed record OvertimeSourceSnapshot(OvertimeSourceResponse Source, Guid? CompensationRecordId, OvertimeEmploymentSource Employment,
    IReadOnlyList<AttendanceResourceScope> OriginalScopes);
public sealed record OvertimeSubmissionSnapshot(int SchemaVersion, Guid SubmissionId, Guid EmployeeId,
    Guid RequesterAccountId, SubmitOvertimeRequest Proposal, OvertimeSourceSnapshot Inputs);
public sealed record OvertimeDecisionSnapshot(int SchemaVersion, Guid RequestId, Guid EmployeeId,
    string SourceFingerprint, ApprovalState Decision, int? ApprovedMinutes, Guid? PolicyVersionId,
    string? CalculationVersion, Guid? EligibilityVersionId, Guid? CompensationRecordId,
    string? RateRuleCode, string FinancialState, long? ApprovedTicks = null, OvertimeFinancialEvidence? FinancialEvidence = null);
public sealed record OvertimeFinancialEvidence(string FormulaVersion, Guid CompensationRecordId, DateOnly SourceBusinessDate,
    decimal MonthlyBaseSalary, int SalaryCalculationDays, long ApprovedTicks, decimal Amount, string Currency);
public sealed record OvertimeEmploymentSource(Guid AssignmentId, Guid StaffCategoryId, string StaffCategory,
    Guid? SiteId, DateOnly EffectiveFrom, DateOnly? EffectiveTo,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] Guid? BranchId = null);
public sealed record OvertimeRecord
{
    public OvertimeRequest Request { get; init; } = null!;
    public ApprovalRequest Approval { get; init; } = null!;
    public ApprovalDecision? Decision { get; init; }
    public DateOnly BusinessDate { get; init; }
}
public interface IOvertimeEligibilityReader
{
    Task<OvertimeEligibility> ResolveAsync(Guid employeeId, DateOnly date, OvertimeEmploymentSource employment, CancellationToken ct);
}
public interface IOvertimeDurationPolicy
{
    // An approved adapter must prove completed duty, paid/unpaid break treatment and exact qualifying
    // intervals. A generic daily total or a salary value cannot stand in for this interpretation.
    Task<OvertimeDurationResult> EvaluateAsync(Guid employeeId, DateOnly date,
        IReadOnlyList<OvertimeSessionSource> sessions, EffectiveRosterResponse roster, CancellationToken ct);
}
public sealed class UnavailableOvertimeEligibilityReader : IOvertimeEligibilityReader
{
    public Task<OvertimeEligibility> ResolveAsync(Guid employeeId, DateOnly date, OvertimeEmploymentSource employment, CancellationToken ct) =>
        Task.FromResult(new OvertimeEligibility(employment.StaffCategory == "OFFICE" ? "office_ineligible" : "eligibility_not_configured", null,
            employment.StaffCategory == "OFFICE" ? false : null));
}
public sealed class UnavailableOvertimeDurationPolicy : IOvertimeDurationPolicy
{
    public Task<OvertimeDurationResult> EvaluateAsync(Guid employeeId, DateOnly date,
        IReadOnlyList<OvertimeSessionSource> sessions, EffectiveRosterResponse roster, CancellationToken ct) =>
        Task.FromResult(new OvertimeDurationResult("duration_policy_unavailable", null, null, null, []));
}
public interface IOvertimeRepository
{
    Task<OvertimeEmploymentSource?> EmploymentAsync(Guid employeeId, DateOnly date, CancellationToken ct);
    Task<IReadOnlyList<OvertimeSessionSource>> SessionSourcesAsync(Guid employeeId, DateOnly date, CancellationToken ct);
    Task<bool> ApprovedPolicyAsync(Guid policyVersionId, DateOnly date, CancellationToken ct);
    Task<OvertimeRecord?> FindAsync(Guid employeeId, Guid id, CancellationToken ct);
    Task<IReadOnlyList<OvertimeRecord>> CandidatesAsync(OvertimeQueueRequest request, AttendanceCursorPosition? after,
        int limit, Guid? requesterId, CancellationToken ct);
    Task<bool> HasConflictingRequestAsync(Guid employeeId, DateTimeOffset start, DateTimeOffset end, Guid? exceptId, CancellationToken ct);
    void Add(OvertimeRequest request, ApprovalRequest approval);
    void Add(ApprovalDecision decision);
    void ExpectVersion(ApprovalRequest approval, byte[] version);
    Task SaveChangesAsync(CancellationToken ct);
}
