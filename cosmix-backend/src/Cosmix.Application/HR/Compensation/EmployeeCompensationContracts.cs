using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Cosmix.Application.Common.Models;
using Cosmix.Domain.Common.Approvals;
using Cosmix.Domain.HR.Compensation;

namespace Cosmix.Application.HR.Compensation;

public static class EmployeeCompensationPermissions
{
    public const string Read = "hr.employees.compensation.read";
    public const string Request = "hr.employees.compensation.request";
    public const string Review = "hr.employees.compensation.review";
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record RequestCompensationChange(
    [Required, StringLength(12, MinimumLength = 12)] string ExpectedVersion,
    Guid? ExpectedCompensationId, [Required] decimal? BaseAmount, [Required] decimal? GrossAmount,
    [Required, StringLength(3, MinimumLength = 3)] string Currency,
    WageBasis WageBasis, PaymentMethod PaymentMethod, DateOnly EffectiveFrom,
    [Required, StringLength(1000)] string Reason);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record DecideCompensationChange(
    [Required, StringLength(12, MinimumLength = 12)] string ExpectedRequestVersion,
    [Required, StringLength(12, MinimumLength = 12)] string ExpectedEmployeeVersion,
    ApprovalState Decision, [StringLength(2000)] string? Reason = null);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record CancelCompensationChange(
    [Required, StringLength(12, MinimumLength = 12)] string ExpectedRequestVersion);

public sealed record CompensationPageQuery([Range(1, 1000000)] int Page = 1, [Range(1, 100)] int PageSize = 25);
public sealed record CompensationRequestQuery([Range(1, 1000000)] int Page = 1,
    [Range(1, 100)] int PageSize = 25, ApprovalState? State = null);
public sealed record CompensationTerms(decimal BaseAmount, decimal GrossAmount, string Currency,
    WageBasis WageBasis, PaymentMethod PaymentMethod);

// EffectiveTo is resolved from the next version; RecordedEffectiveTo preserves the stored evidence.
public sealed record CompensationEntry(Guid Id, int Revision, Guid? SupersedesCompensationRecordId,
    CompensationTerms Terms, DateOnly EffectiveFrom, DateOnly? EffectiveTo, DateOnly? RecordedEffectiveTo,
    Guid? ApprovalRequestId, string RecordedByUserId, DateTimeOffset RecordedAt);
public sealed record EmployeeCompensationState(Guid EmployeeId, string EmployeeVersion, DateOnly AsOf,
    CompensationEntry? Effective, CompensationEntry? NextScheduled, Guid? LatestRecordId);
public sealed record CompensationRequestSummary(Guid Id, Guid EmployeeId, ApprovalState State,
    string RequesterAccountId, DateTimeOffset RequestedAt, DateTimeOffset? ClosedAt, string Version);
public sealed record CompensationDecision(Guid Id, ApprovalState Decision, string ActorAccountId,
    string ActorRoleCode, string? Reason, DateTimeOffset DecidedAt);
public sealed record CompensationRequestDetail(CompensationRequestSummary Request, CompensationEntry? Before,
    CompensationTerms Proposed, DateOnly EffectiveFrom, string Reason, IReadOnlyList<CompensationDecision> Decisions);

// Protected internal evidence, never accepted from a client or exposed with storage references.
public sealed record CompensationChangeSnapshot(int SchemaVersion, Guid EmployeeId, CompensationEntry? Before,
    CompensationTerms Proposed, DateOnly EffectiveFrom, string Reason);
public sealed record CompensationWindow(CompensationRecord? Candidate, CompensationRecord? Next, CompensationRecord? Latest);

public interface IEmployeeCompensationRepository
{
    Task<bool> HasConflictingHistoryAsync(Guid employeeId, CancellationToken ct);
    Task<CompensationWindow> WindowAsync(Guid employeeId, DateOnly asOf, CancellationToken ct);
    Task<PagedResult<CompensationEntry>> HistoryAsync(Guid employeeId, CompensationPageQuery query, CancellationToken ct);
    Task<bool> HasPendingRequestAsync(Guid employeeId, CancellationToken ct);
    Task<ApprovalRequest?> FindRequestAsync(Guid employeeId, Guid requestId, CancellationToken ct);
    Task<IReadOnlyList<ApprovalDecision>> DecisionsAsync(Guid requestId, CancellationToken ct);
    Task<PagedResult<CompensationRequestSummary>> RequestsAsync(Guid? employeeId, CompensationRequestQuery query, CancellationToken ct);
    void ExpectVersion(ApprovalRequest request, byte[] version);
    void AddRequest(ApprovalRequest request);
    void AddDecision(ApprovalDecision decision);
    void AddCompensation(CompensationRecord record);
}
