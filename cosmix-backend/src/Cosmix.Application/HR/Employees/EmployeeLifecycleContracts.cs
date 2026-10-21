using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Cosmix.Application.Common.Models;
using Cosmix.Domain.Authentication;
using Cosmix.Domain.Common.Approvals;
using Cosmix.Domain.HR.Employees;

namespace Cosmix.Application.HR.Employees;

public static class EmployeeLifecyclePermissions
{
    public const string Read = "hr.employees.lifecycle.read";
    public const string Request = "hr.employees.lifecycle.request";
    public const string Review = "hr.employees.lifecycle.review";
    public const string Archive = "hr.employees.lifecycle.archive";
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record SetEmployeeArchiveRequest(bool Archived,
    [Required, StringLength(12, MinimumLength = 12)] string ExpectedVersion,
    [Required, StringLength(1000)] string Reason);
public sealed record EmployeeArchiveHistory(Guid Id, bool PreviousArchived, bool IsArchived,
    EmployeeStatus EmploymentStatus, string Reason, string ActorAccountId, string ActorRoleCode, DateTimeOffset RecordedAt);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record RequestEmployeeStatusChange(
    [Required, StringLength(12, MinimumLength = 12)] string ExpectedVersion,
    EmployeeStatus NewStatus, DateTimeOffset? EffectiveAt,
    [Required, StringLength(1000)] string Reason);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record DecideEmployeeStatusChange(
    [Required, StringLength(12, MinimumLength = 12)] string ExpectedRequestVersion,
    [Required, StringLength(12, MinimumLength = 12)] string ExpectedEmployeeVersion,
    ApprovalState Decision, [StringLength(2000)] string? Reason = null);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record CancelEmployeeStatusChange(
    [Required, StringLength(12, MinimumLength = 12)] string ExpectedRequestVersion);

public sealed record EmployeeLifecycleQuery([Range(1, 1000000)] int Page = 1,
    [Range(1, 100)] int PageSize = 25, ApprovalState? State = null);
public sealed record EmployeeLifecycleRequestSummary(Guid Id, Guid EmployeeId, ApprovalState State,
    string RequesterAccountId, DateTimeOffset RequestedAt, DateTimeOffset? ClosedAt, string Version);
public sealed record EmployeeLifecycleDecision(Guid Id, ApprovalState Decision, string ActorAccountId,
    string ActorRoleCode, string? Reason, DateTimeOffset DecidedAt);
public sealed record EmployeeLifecycleRequestDetail(EmployeeLifecycleRequestSummary Request,
    EmployeeStatus PreviousStatus, EmployeeStatus NewStatus, DateTimeOffset? RequestedEffectiveAt,
    string Reason, IReadOnlyList<EmployeeLifecycleDecision> Decisions, string? LifecyclePolicyVersion = null);
public sealed record EmployeeLifecycleHistory(Guid Id, EmployeeStatus PreviousStatus, EmployeeStatus NewStatus,
    DateTimeOffset EffectiveAt, string Reason, string ActorAccountId, DateTimeOffset RecordedAt, Guid? ApprovalRequestId,
    string? LifecyclePolicyVersion = null);
public sealed record EmployeeLifecycleState(Guid EmployeeId, string Version, EmployeeStatus CurrentStatus,
    Guid? CurrentStatusChangeId, EmployeeLifecycleHistory? ScheduledChange, bool IsArchived = false, DateTimeOffset? ArchivedAt = null);
public sealed record EmployeeLifecycleBatchResult(int Applied, long NextEmployeeNumber, int ReviewRequired);

// Internal immutable snapshot, never accepted from a client or returned with storage references.
public sealed record EmployeeLifecycleSnapshot(int SchemaVersion, Guid EmployeeId, EmployeeStatus PreviousStatus,
    Guid? PreviousChangeId, EmployeeStatus NewStatus, DateTimeOffset? EffectiveAt, string Reason, string? LifecyclePolicyVersion = null);

public interface IEmployeeLifecycleRepository
{
    Task<bool> HasApprovedEmploymentAsync(Employee employee, CancellationToken ct);
    Task<UserAccount?> FindAccountAsync(Guid employeeId, CancellationToken ct);
    /// <summary>Latest two due rows, to detect ambiguous effective instants.</summary>
    Task<IReadOnlyList<EmployeeStatusChange>> CurrentChangesAsync(Guid employeeId, DateTimeOffset now, CancellationToken ct);
    /// <summary>Due rows after the recorded applied marker in chronological order; never skip a suspension/release cycle.</summary>
    Task<IReadOnlyList<EmployeeStatusChange>> UnappliedChangesAsync(Guid employeeId, Guid? appliedChangeId, DateTimeOffset now, CancellationToken ct);
    Task<IReadOnlyList<RegisteredDevice>> UnrevokedDevicesAsync(Guid accountId, CancellationToken ct);
    Task<IReadOnlyList<UserRoleAssignment>> UnrevokedRoleAssignmentsAsync(Guid accountId, CancellationToken ct);
    /// <summary>Logical revocation only; encrypted template disposal and retention remain the Attendance lifecycle responsibility.</summary>
    Task RevokeFaceEnrollmentsAsync(Guid employeeId, Guid statusChangeId, DateTimeOffset now, CancellationToken ct);
    Task<bool> HasFutureChangeAsync(Guid employeeId, DateTimeOffset now, CancellationToken ct);
    Task<EmployeeStatusChange?> NextChangeAsync(Guid employeeId, DateTimeOffset now, CancellationToken ct);
    Task<bool> HasPendingRequestAsync(Guid employeeId, CancellationToken ct);
    Task<ApprovalRequest?> FindRequestAsync(Guid employeeId, Guid requestId, CancellationToken ct);
    Task<IReadOnlyList<ApprovalDecision>> DecisionsAsync(Guid requestId, CancellationToken ct);
    Task<PagedResult<EmployeeLifecycleRequestSummary>> RequestsAsync(Guid? employeeId, EmployeeLifecycleQuery query, CancellationToken ct);
    Task<PagedResult<ApprovalRequest>> OwnRequestsAsync(Guid employeeId, string requesterAccountId, int page, int pageSize, CancellationToken ct);
    Task<PagedResult<EmployeeLifecycleHistory>> HistoryAsync(Guid employeeId, EmployeeAssignmentListRequest query, CancellationToken ct);
    Task<PagedResult<EmployeeArchiveHistory>> ArchiveHistoryAsync(Guid employeeId, EmployeeAssignmentListRequest query, CancellationToken ct);
    /// <summary>Bounded candidate employee IDs with approved due changes not reflected in cached employee status.</summary>
    Task<IReadOnlyList<Guid>> DueEmployeesAsync(DateTimeOffset now, int limit, CancellationToken ct, long afterEmployeeNumber = 0);
    void ExpectVersion(ApprovalRequest request, byte[] version);
    void AddRequest(ApprovalRequest request);
    void AddDecision(ApprovalDecision decision);
    void AddStatusChange(EmployeeStatusChange change);
    void AddArchiveChange(EmployeeArchiveChange change);
}
