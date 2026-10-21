using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Cosmix.Application.Authentication.Access;
using Cosmix.Application.Common.Models;
using Cosmix.Domain.Common.Approvals;
using Cosmix.Domain.HR.Leaves;
using Cosmix.Domain.HR.Policies;

namespace Cosmix.Application.HR.Leaves;

public static class LeavePermissions
{
    public const string Self = "attendance.leaves.self";
    public const string Review = "attendance.leaves.review";
    public const string Configure = "attendance.leaves.configure";
    public const string EncashmentSubmit = "attendance.leave.encashment.submit";
    public const string EncashmentMine = "attendance.leave.encashment.mine";
    public const string EncashmentQueue = "attendance.leave.encashment.queue";
    public const string EncashmentDecide = "attendance.leave.encashment.decide";

    public static IReadOnlyList<AccessPermissionDefinition> Definitions { get; } = [
        new(Self, "Apply for, read and request cancellation of only my leave", false, false, true),
        new(Review, "Review leave directly as a fixed Admin/SuperAdmin with explicit permission", true, true, false),
        new(Configure, "Publish explicit leave settings and manage leave types as fixed Admin/SuperAdmin with matching explicit All permission", true, true, false),
        new(EncashmentSubmit, "Submit leave encashment request", false, false, true),
        new(EncashmentMine, "Read own leave encashment requests", false, false, true),
        new(EncashmentQueue, "Read pending leave encashment queue as Admin", true, true, false),
        new(EncashmentDecide, "Approve or reject leave encashment as fixed Admin/SuperAdmin", true, true, false)
    ];
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record LeaveDayRequest(DateOnly Date, DateTimeOffset StartsAtUtc, DateTimeOffset EndsAtUtc);
public enum LeaveEvidenceKind { Medical = 1, AdmitCard = 2, DateSheet = 3, Emergency = 4, InjuryMedical = 5, Other = 6 }
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record LeaveAttachmentRequest(LeaveEvidenceKind Kind, string FileName, string ContentType, byte[] Content);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record SubmitLeaveRequest(Guid LeaveTypeId, [Required] IReadOnlyList<LeaveDayRequest> Days,
    [Required, StringLength(1000)] string Reason, string? ContactDuringLeave = null, Guid? RelieverEmployeeId = null,
    string? HandoverNotes = null, IReadOnlyList<LeaveAttachmentRequest>? Attachments = null,
    Guid? PreviousRequestId = null);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record DecideLeaveRequest(ApprovalState Decision, string ExpectedApprovalVersion, string Reason,
    LeavePayTreatment Treatment = LeavePayTreatment.PendingReview, Guid? PolicyVersionId = null);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record RequestLeaveCancellation(string ExpectedApprovalVersion, string Reason);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record DecideLeaveCancellation(ApprovalState Decision, string ExpectedApprovalVersion, string Reason);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record CreateLeaveTypeRequest(string Code, string Name, LeaveKind Kind, bool RequiresEvidence,
    bool RequiresAdvanceApproval, bool ReducesRegularBalance, string Reason);
public sealed record LeaveListRequest(DateOnly From, DateOnly To, int Page = 1, int PageSize = 25, bool PendingOnly = false);
public sealed record LeaveTypeResponse(Guid Id, string Code, string Name, LeaveKind Kind, bool RequiresEvidence,
    bool RequiresAdvanceApproval, bool ReducesRegularBalance, bool IsActive, string Version);
public sealed record LeaveDayResponse(Guid Id, DateOnly Date, DateTimeOffset StartsAtUtc, DateTimeOffset EndsAtUtc, int Minutes);
public sealed record LeaveDecisionResponse(Guid Id, Guid ApprovalRequestId, ApprovalState Decision, string ActorRole,
    string? Reason, DateTimeOffset AtUtc);
public sealed record LeaveCancellationResponse(Guid Id, Guid ApprovalRequestId, ApprovalState State, string Reason,
    string ApprovalVersion, DateTimeOffset RequestedAtUtc, IReadOnlyList<LeaveDecisionResponse> Decisions);
public sealed record LeaveResponse(Guid Id, Guid EmployeeId, Guid LeaveTypeId, LeaveKind Kind, string TypeName,
    Guid? PreviousRequestId, Guid? SupersededByRequestId, DateOnly StartDate, DateOnly EndDate, int RequestedMinutes,
    string Reason, string? ContactDuringLeave, Guid? RelieverEmployeeId, string? HandoverNotes, bool HasEvidence,
    Guid ApprovalRequestId, ApprovalState State, string ApprovalVersion, LeavePayTreatment Treatment, Guid? PolicyVersionId,
    IReadOnlyList<LeaveDayResponse> Days, IReadOnlyList<LeaveDecisionResponse> Decisions,
    IReadOnlyList<LeaveCancellationResponse> Cancellations, string FinancialStatus);
public sealed record LeaveEvidenceDownload(byte[] Content, string FileName, string ContentType);
public sealed record LeaveRow(LeaveRequest Request, LeaveType Type, ApprovalRequest? Approval,
    IReadOnlyList<ApprovalDecision> Decisions, IReadOnlyList<LeaveCancellationRow> Cancellations, Guid? SupersededByRequestId);
public sealed record LeaveCancellationRow(LeaveCancellationRequest Request, ApprovalRequest? Approval, IReadOnlyList<ApprovalDecision> Decisions);
public sealed record LeaveUsageUnit(int UsedMinutes, int StandardDayMinutes, LeaveKind Kind);
public sealed record LeaveBenefitCredit(Guid AwardId, int Minutes, int StandardDayMinutes, LeaveKind Kind, bool Encashable)
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool ConsumptionOffset { get; init; }
}
public sealed record LeaveBalanceUsage(int AnnualMinutes, int TypeMinutes, IReadOnlyList<LeaveUsageUnit>? Units = null)
{
    // Absent in retained pre-benefit decision proofs; omitting null preserves their hashes.
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<LeaveBenefitCredit>? Credits { get; init; }
}
public sealed record LeavePolicyEvidence(PolicyVersion Version, Guid StaffCategoryId, DateOnly? JoinedOn, bool ProvenanceValid,
    IReadOnlyList<DateOnly> Holidays, LeaveBalanceUsage Usage)
{
    public IReadOnlyList<Guid>? AssignmentIds { get; init; }
}

public interface ILeaveRepository
{
    Task<IReadOnlyList<LeaveType>> TypesAsync(CancellationToken ct);
    Task<LeaveType?> TypeAsync(Guid id, CancellationToken ct);
    Task<bool> TypeCodeExistsAsync(string code, CancellationToken ct);
    Task<bool> SubmissionExistsAsync(Guid employeeId, string key, CancellationToken ct);
    Task<bool> EmployeeExistsAsync(Guid employeeId, CancellationToken ct);
    Task<LeaveRow?> FindAsync(Guid id, Guid? employeeId, CancellationToken ct);
    Task<PagedResult<LeaveRow>> ListAsync(Guid? employeeId, LeaveListRequest request, CancellationToken ct);
    Task<IReadOnlyList<LeaveRow>> ForDateAsync(Guid employeeId, DateOnly date, CancellationToken ct);
    Task<IReadOnlyList<LeaveRow>> OverlappingAsync(Guid employeeId, DateOnly from, DateOnly to, CancellationToken ct);
    Task<LeavePolicyEvidence?> PolicyAsync(Guid employeeId, Guid typeId, Guid policyVersionId, DateOnly from, DateOnly to, CancellationToken ct);
    Task<bool> AnyLockedAsync(DateOnly from, DateOnly to, CancellationToken ct);
    Task<IReadOnlyList<LeaveLedgerEntry>> LedgerAsync(Guid requestId, CancellationToken ct);
    Task<bool> SettingsReferencesValidAsync(LeavePolicySettings settings, CancellationToken ct);
    Task<bool> PolicyCodeExistsAsync(string code, CancellationToken ct);
    Task<IReadOnlyList<PolicyVersion>> PolicyVersionsAsync(CancellationToken ct);
    Task<PolicyVersion?> PolicyVersionAsync(Guid id, CancellationToken ct);
    Task<IReadOnlyList<DateOnly>> HolidaysAsync(Guid calendarId, DateOnly from, DateOnly to, CancellationToken ct);
    void Add(LeaveType type);
    void Add(LeaveRequest request, IReadOnlyList<LeaveRequestDay> days, ApprovalRequest approval);
    void Add(LeaveCancellationRequest cancellation, ApprovalRequest approval);
    void Add(ApprovalDecision decision);
    void Add(LeaveLedgerEntry entry);
    void Add(Policy policy, PolicyVersion version);
    void ExpectVersion(ApprovalRequest approval, byte[] version);
    Task SaveChangesAsync(CancellationToken ct);
}

public enum LeaveContextStatus { Complete = 1, Pending = 2, Conflict = 3 }
public sealed record ApprovedLeaveDayEffect(Guid LeaveRequestId, Guid AllocationId, Guid ApprovalRequestId,
    Guid ApprovalDecisionId, Guid? PolicyVersionId, DateOnly Date, DateTimeOffset StartsAtUtc,
    DateTimeOffset EndsAtUtc, int Minutes, LeavePayTreatment Treatment, Guid? ShiftVersionId = null);
public sealed record LeaveAttendanceContext(LeaveContextStatus Status, IReadOnlyList<ApprovedLeaveDayEffect> Effects,
    string Digest, IReadOnlyList<string> Reasons);
/// <summary>Internal authorized-calculation seam. Empty Complete means the bounded query verified no active leave.
/// Pending/conflict is explicit. Callers must not synthesize zero or divide total request minutes among days.</summary>
public interface ILeaveAttendanceContextReader
{
    Task<LeaveAttendanceContext> ReadAsync(Guid employeeId, DateOnly date, CancellationToken ct);
}
