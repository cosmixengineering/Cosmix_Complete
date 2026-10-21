using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Cosmix.Application.Attendance.Overtime;
using Cosmix.Application.Authentication.Access;
using Cosmix.Domain.Common.Approvals;
using Cosmix.Domain.HR.Allowances;

namespace Cosmix.Application.HR.Allowances;

public static class AllowancePermissions
{
    public const string Self = "attendance.allowances.self";
    public const string Submit = "attendance.allowances.submit";
    public const string Review = "attendance.allowances.review";
    public const string Decide = "attendance.allowances.decide";
    public const string Policy = "attendance.allowances.policy";
    public const string Settlement = "attendance.allowances.settlement";
    public static IReadOnlyList<AccessPermissionDefinition> Definitions { get; } = [
        new(Self, "Read own allowance eligibility and submit, read or cancel own allowance claims", false, false, true),
        new(Submit, "Preview eligibility and submit explicit employee allowance claims within scope", false, true, false),
        new(Review, "Review scoped allowance claims and their retained duty evidence", true, true, false),
        new(Decide, "Make a separate HR allowance decision within scope", true, true, false),
        new(Policy, "Publish and assign effective-dated allowance policy amounts as fixed Admin/SuperAdmin", true, true, false),
        new(Settlement, "Record Accounts payroll-handoff or paid settlement evidence for an approved allowance claim", true, true, false)
    ];
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record AllowanceSubmitRequest(AllowanceKind Kind, DateOnly BusinessDate,
    [Required, StringLength(1000)] string Reason);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record AllowanceCancelRequest(
    [Required, StringLength(12, MinimumLength = 12)] string ExpectedVersion,
    [Required, StringLength(1000)] string Reason);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record AllowanceDecideRequest(
    [Required, StringLength(12, MinimumLength = 12)] string ExpectedVersion,
    ApprovalState Decision, [Required, StringLength(1000)] string Reason);

/// <summary>Accounts records an external payment against an approved claim already scheduled by a
/// locked payroll handoff. It never initiates a payment and never revises the approved amount.</summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record AllowanceSettlementRequest(
    [Required, StringLength(12, MinimumLength = 12)] string ExpectedVersion,
    [Required, StringLength(150)] string ExternalPaymentReference,
    [Required, StringLength(1000)] string Reason);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record AllowanceHistoryRequest(DateOnly From, DateOnly To, ApprovalState? State = null,
    int Page = 1, int PageSize = 25);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record AllowanceQueueRequest(DateOnly From, DateOnly To, Guid? EmployeeId = null,
    ApprovalState? State = ApprovalState.Pending, int Page = 1, int PageSize = 25);

/// <summary>Server-evaluated duty cycle for one kind. ClaimedAmount is the published policy amount,
/// never a salary value, and is null while management has not confirmed the kind's amount.</summary>
public sealed record AllowanceKindEligibility(AllowanceKind Kind, string State, decimal? ContinuousHours,
    decimal? RequiredHours, DateTimeOffset? EligibilityPeriodStartUtc, DateTimeOffset? EligibilityPeriodEndUtc,
    Guid? WorkSessionId, string? EligibilityKey, decimal? ClaimedAmount, string? Currency, bool ClaimExists,
    string Reason);

public sealed record AllowanceSourceResponse(Guid EmployeeId, DateOnly BusinessDate, string StaffCategory,
    string PolicyState, Guid? PolicyVersionId, IReadOnlyList<Guid> AssignmentIds, string? Currency,
    IReadOnlyList<AllowanceKindEligibility> Kinds);

public sealed record AllowanceClaimResponse(Guid Id, Guid EmployeeId, AllowanceKind Kind, string State,
    string Version, int SubmissionVersion, decimal ClaimedAmount, string Currency, DateOnly PayableDate,
    DateOnly? PayrollPeriodStart, Guid PolicyVersionId, string SettlementState, Guid? ApprovalRequestId,
    Guid? DecisionId, Guid? WorkSessionId, string DeploymentCategoryCode, DateTimeOffset EligibilityPeriodStart,
    DateTimeOffset EligibilityPeriodEnd, string EligibilityKey, DateTimeOffset CreatedAt, DateTimeOffset? SubmittedAt,
    DateTimeOffset? DecidedAt);

public sealed record AllowanceDetail(AllowanceClaimResponse Claim, AllowanceKindEligibility Eligibility,
    string? Reason, string Visibility);

public sealed record AllowancePage(IReadOnlyList<AllowanceClaimResponse> Items, int Page, int PageSize, bool HasMore);

/// <summary>Protected submission content written to private storage under a server-selected purpose.
/// It is never accepted from HTTP and never returned to clients.</summary>
public sealed record AllowanceSubmissionSnapshot(int SchemaVersion, Guid EmployeeId, Guid RequesterAccountId,
    AllowanceSubmitRequest Proposal, AllowanceKindEligibility Eligibility, OvertimeEmploymentSource Employment,
    Guid PolicyVersionId, string PolicyState, IReadOnlyList<Guid> AssignmentIds);

public sealed record AllowanceRecord
{
    public AllowanceClaim Claim { get; init; } = null!;
    public ApprovalRequest Approval { get; init; } = null!;
    public ApprovalDecision? Decision { get; init; }
    public IReadOnlyList<AllowanceClaimSubmission> Submissions { get; init; } = [];
}

public sealed record AllowancePageQuery(Guid? EmployeeId, DateOnly? From, DateOnly? To, ApprovalState? State,
    int Skip, int Limit);

public interface IAllowanceRepository
{
    Task<OvertimeEmploymentSource?> EmploymentAsync(Guid employeeId, DateOnly date, CancellationToken ct);
    Task<IReadOnlyList<OvertimeSessionSource>> SessionSourcesAsync(Guid employeeId, DateOnly date, CancellationToken ct);
    Task<IReadOnlyList<AllowanceKind>> KindsClaimedAsync(Guid employeeId, DateOnly payableDate, CancellationToken ct);
    Task<bool> KeyClaimExistsAsync(Guid employeeId, AllowanceKind kind, string eligibilityKey, CancellationToken ct);
    Task<bool> DateClaimExistsAsync(Guid employeeId, AllowanceKind kind, DateOnly payableDate, CancellationToken ct);
    Task<bool> DeploymentClaimOverlapAsync(Guid employeeId, DateTimeOffset from, DateTimeOffset to, CancellationToken ct);
    Task<AllowanceRecord?> FindAsync(Guid employeeId, Guid id, CancellationToken ct);
    Task<IReadOnlyList<AllowanceRecord>> PageAsync(AllowancePageQuery query, Guid? requesterId, CancellationToken ct);
    void Add(AllowanceClaim claim, ApprovalRequest approval, AllowanceClaimSubmission submission);
    void Add(ApprovalDecision decision);
    void ExpectVersion(ApprovalRequest approval, byte[] version);
    Task SaveChangesAsync(CancellationToken ct);
}

/// <summary>Settlement history is append-only and written by the settlement authority that also owns
/// the payroll handoff batch, so a handoff line and its allowance settlement share one transaction.</summary>
public interface IAllowanceSettlementStore
{
    Task TrackApprovalEvidenceAsync(Guid approvalRequestId, CancellationToken ct);
    void Add(AllowanceSettlementRecord record);
}
