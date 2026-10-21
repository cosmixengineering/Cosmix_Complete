using System.Text.Json;
using Cosmix.Application.Authentication;
using Cosmix.Application.Authentication.Access;
using Cosmix.Application.Common.Files;
using Cosmix.Application.Common.Models;
using Cosmix.Application.HR.Organization;
using Cosmix.Domain.Common.Approvals;
using Cosmix.Domain.HR.Employees;

namespace Cosmix.Application.HR.Employees;

public sealed class EmployeeLifecycleService(EmployeeOperation operation, IEmployeeHiringRepository hiring,
    IEmployeeLifecycleRepository repository, IAccessManagementRepository access, IPrivateFileStore files,
    EmployeeLifecycleProcessor processor, EmployeeLifecyclePolicy policy)
{
    public const string RequestType = "EmployeeStatusChange";

    public Task<EmployeeWriteResult> ArchiveAsync(Guid employeeId, SetEmployeeArchiveRequest input,
        string key, AuthRequestContext context, CancellationToken ct) =>
        operation.WriteAsync([EmployeeLifecyclePermissions.Archive, EmployeeLifecyclePermissions.Read],
            "hr.employee.lifecycle.archive_changed", employeeId, key, input, context, async (actor, token) =>
            {
                OrganizationOperation.Validate(input);
                ValidateReason(input.Reason, required: true, maximumLength: 1000);
                var employee = await EmployeeAsync(employeeId, token);
                hiring.ExpectVersion(employee, OrganizationOperation.Version(input.ExpectedVersion));
                await processor.ApplyDueAsync(employee, actor.Now, token);
                if (await repository.HasPendingRequestAsync(employeeId, token) ||
                    await repository.HasFutureChangeAsync(employeeId, actor.Now, token))
                    throw EmployeeOperation.Conflict("employee_lifecycle_request_or_change_outstanding");
                EmployeeArchiveChange change;
                try { change = employee.SetArchived(input.Archived, input.Reason.Trim(), actor.Identity.AccountId.ToString(), actor.RoleCode, actor.Now); }
                catch (InvalidOperationException) { throw EmployeeOperation.Conflict("employee_archive_requires_ended_employment_and_changed_state"); }
                repository.AddArchiveChange(change);
                await hiring.SaveChangesAsync(token);
                return EmployeeHiringService.Result(employee) with { ArchiveChangeId = change.Id };
            }, ct, fixedAdminPermission: EmployeeLifecyclePermissions.Archive, authorizeResource: async (actor, token) =>
            {
                if (actor.Identity.EmployeeId == employeeId) throw EmployeeOperation.Denied();
                await RequireUnprotectedTargetAsync(employeeId, actor.Now, token);
            });

    public Task<PagedResult<EmployeeArchiveHistory>> ArchiveHistoryAsync(Guid employeeId, EmployeeAssignmentListRequest input,
        AuthRequestContext context, CancellationToken ct) =>
        operation.ReadAsync([EmployeeLifecyclePermissions.Read], "hr.employee.lifecycle.archive_history_read", employeeId,
            context, async (_, token) =>
            {
                OrganizationOperation.Validate(input);
                ValidatePage(input.Page, input.PageSize);
                await EmployeeAsync(employeeId, token);
                return await repository.ArchiveHistoryAsync(employeeId, input, token);
            }, ct);

    public Task<EmployeeWriteResult> RequestAsync(Guid employeeId, RequestEmployeeStatusChange input,
        string key, AuthRequestContext context, CancellationToken ct) =>
        operation.WriteAsync([EmployeeLifecyclePermissions.Request, EmployeeLifecyclePermissions.Read],
            "hr.employee.lifecycle.requested", employeeId, key, input, context, async (actor, token) =>
            {
                OrganizationOperation.Validate(input);
                ValidateReason(input.Reason, required: true, maximumLength: 1000);
                if (input.EffectiveAt.HasValue && input.EffectiveAt <= actor.Now)
                    throw EmployeeOperation.Invalid("lifecycle_effective_time_must_be_future_or_null");
                var employee = await EmployeeAsync(employeeId, token);
                hiring.ExpectVersion(employee, OrganizationOperation.Version(input.ExpectedVersion));
                await RequireUnprotectedTargetAsync(employeeId, actor.Now, token);
                await processor.ApplyDueAsync(employee, actor.Now, token);
                var current = await CurrentAsync(employee, actor.Now, token);
                if (employee.IsArchived) throw EmployeeOperation.Conflict("archived_employee_lifecycle_unavailable");
                var policyVersion = await policy.ValidateTransitionAsync(current.Status, input.NewStatus, actor.Now, input.EffectiveAt, token);
                if (await repository.HasPendingRequestAsync(employeeId, token) ||
                    await repository.HasFutureChangeAsync(employeeId, actor.Now, token))
                    throw EmployeeOperation.Conflict("employee_lifecycle_request_or_change_outstanding");
                var snapshot = new EmployeeLifecycleSnapshot(2, employeeId, current.Status, current.ChangeId,
                    input.NewStatus, input.EffectiveAt?.ToUniversalTime(), input.Reason.Trim(), policyVersion);
                var evidence = await files.WriteAsync(JsonSerializer.SerializeToUtf8Bytes(snapshot), Purpose(employeeId), token);
                var request = new ApprovalRequest(RequestType, employeeId, actor.Identity.AccountId.ToString(),
                    JsonSerializer.Serialize(evidence), 1, actor.Now);
                repository.AddRequest(request);
                await hiring.SaveChangesAsync(token);
                return EmployeeHiringService.Result(employee) with { LifecycleRequestId = request.Id };
            }, ct);

    public Task<EmployeeWriteResult> DecideAsync(Guid employeeId, Guid requestId, DecideEmployeeStatusChange input,
        string key, AuthRequestContext context, CancellationToken ct) =>
        operation.WriteAsync([EmployeeLifecyclePermissions.Review, EmployeeLifecyclePermissions.Read],
            "hr.employee.lifecycle.decided", employeeId, key, new { requestId, input }, context, async (actor, token) =>
            {
                OrganizationOperation.Validate(input);
                if (input.Decision is not (ApprovalState.Approved or ApprovalState.Returned or ApprovalState.Rejected))
                    throw EmployeeOperation.Invalid("invalid_lifecycle_decision");
                ValidateReason(input.Reason, input.Decision != ApprovalState.Approved);
                var employee = await EmployeeAsync(employeeId, token);
                hiring.ExpectVersion(employee, OrganizationOperation.Version(input.ExpectedEmployeeVersion));
                var request = await RequestAsync(employeeId, requestId, token);
                repository.ExpectVersion(request, OrganizationOperation.Version(input.ExpectedRequestVersion));
                if (request.State != ApprovalState.Pending) throw EmployeeOperation.Conflict("lifecycle_request_already_closed");
                if (request.RequesterUserId == actor.Identity.AccountId.ToString() || employeeId == actor.Identity.EmployeeId)
                    throw EmployeeOperation.Denied();
                EmployeeStatusChange? change = null;
                if (input.Decision == ApprovalState.Approved)
                {
                    await RequireUnprotectedTargetAsync(employeeId, actor.Now, token);
                    var snapshot = await SnapshotAsync(request, token);
                    await processor.ApplyDueAsync(employee, actor.Now, token);
                    var current = await CurrentAsync(employee, actor.Now, token);
                    if (current.Status != snapshot.PreviousStatus || current.ChangeId != snapshot.PreviousChangeId ||
                        await repository.HasFutureChangeAsync(employeeId, actor.Now, token))
                        throw EmployeeOperation.Conflict("employee_lifecycle_changed_request_again");
                    if (employee.IsArchived) throw EmployeeOperation.Conflict("archived_employee_lifecycle_unavailable");
                    var policyVersion = await policy.ValidateTransitionAsync(current.Status, snapshot.NewStatus, actor.Now, snapshot.EffectiveAt, token);
                    if (policyVersion != snapshot.LifecyclePolicyVersion)
                        throw EmployeeOperation.Conflict("employee_lifecycle_policy_changed_request_again");
                    if (snapshot.EffectiveAt.HasValue && snapshot.EffectiveAt <= actor.Now)
                        throw EmployeeOperation.Conflict("lifecycle_effective_time_elapsed_return_request");
                    if ((snapshot.EffectiveAt ?? actor.Now) <= current.EffectiveAt)
                        throw EmployeeOperation.Conflict("lifecycle_effective_time_must_follow_history");
                    change = new EmployeeStatusChange(employeeId, current.Status, snapshot.NewStatus,
                        snapshot.EffectiveAt ?? actor.Now, snapshot.Reason, actor.Identity.AccountId.ToString(), actor.Now, request.Id, policyVersion);
                }
                var decision = request.Decide(input.Decision, actor.Identity.AccountId.ToString(), actor.RoleCode,
                    input.Reason?.Trim(), actor.Now);
                repository.AddDecision(decision);
                if (change is not null)
                {
                    repository.AddStatusChange(change);
                    if (change.EffectiveAt <= actor.Now) await processor.ApplyAsync(employee, change, actor.Now, token);
                }
                await hiring.SaveChangesAsync(token);
                return EmployeeHiringService.Result(employee) with
                { LifecycleRequestId = request.Id, DecisionId = decision.Id, StatusChangeId = change?.Id };
            }, ct, fixedAdminPermission: EmployeeLifecyclePermissions.Review);

    public Task<EmployeeWriteResult> CancelAsync(Guid employeeId, Guid requestId, CancelEmployeeStatusChange input,
        string key, AuthRequestContext context, CancellationToken ct) =>
        operation.WriteAsync([EmployeeLifecyclePermissions.Request, EmployeeLifecyclePermissions.Read],
            "hr.employee.lifecycle.cancelled", employeeId, key, new { requestId, input }, context, async (actor, token) =>
            {
                OrganizationOperation.Validate(input);
                var employee = await EmployeeAsync(employeeId, token);
                var request = await RequestAsync(employeeId, requestId, token);
                repository.ExpectVersion(request, OrganizationOperation.Version(input.ExpectedRequestVersion));
                if (request.RequesterUserId != actor.Identity.AccountId.ToString()) throw EmployeeOperation.Denied();
                if (request.State != ApprovalState.Pending) throw EmployeeOperation.Conflict("lifecycle_request_already_closed");
                request.Cancel(actor.Identity.AccountId.ToString(), actor.Now);
                await hiring.SaveChangesAsync(token);
                return EmployeeHiringService.Result(employee) with { LifecycleRequestId = request.Id };
            }, ct);

    public Task<EmployeeLifecycleState> StateAsync(Guid employeeId, AuthRequestContext context, CancellationToken ct) =>
        operation.ReadAsync([EmployeeLifecyclePermissions.Read], "hr.employee.lifecycle.state_read", employeeId,
            context, async (actor, token) =>
            {
                var employee = await EmployeeAsync(employeeId, token);
                var current = await CurrentAsync(employee, actor.Now, token);
                var next = await repository.NextChangeAsync(employeeId, actor.Now, token);
                return new EmployeeLifecycleState(employeeId, Convert.ToBase64String(employee.RowVersion), current.Status,
                    current.ChangeId, next is null ? null : Map(next), employee.IsArchived, employee.ArchivedAt);
            }, ct);

    public Task<PagedResult<EmployeeLifecycleRequestSummary>> RequestsAsync(Guid? employeeId, EmployeeLifecycleQuery input,
        AuthRequestContext context, CancellationToken ct) =>
        operation.ReadAsync([EmployeeLifecyclePermissions.Read], "hr.employee.lifecycle.requests_read", employeeId,
            context, async (_, token) =>
            {
                OrganizationOperation.Validate(input);
                ValidatePage(input.Page, input.PageSize);
                if (input.State.HasValue && !Enum.IsDefined(input.State.Value)) throw EmployeeOperation.Invalid("invalid_lifecycle_state");
                if (employeeId.HasValue) await EmployeeAsync(employeeId.Value, token);
                return await repository.RequestsAsync(employeeId, input, token);
            }, ct);

    public Task<EmployeeLifecycleRequestDetail> DetailAsync(Guid employeeId, Guid requestId, AuthRequestContext context, CancellationToken ct) =>
        operation.ReadAsync([EmployeeLifecyclePermissions.Read], "hr.employee.lifecycle.request_read", employeeId,
            context, async (_, token) =>
            {
                var request = await RequestAsync(employeeId, requestId, token);
                var snapshot = await SnapshotAsync(request, token);
                var decisions = await repository.DecisionsAsync(requestId, token);
                return new EmployeeLifecycleRequestDetail(new(request.Id, employeeId, request.State, request.RequesterUserId,
                    request.RequestedAt, request.ClosedAt, Convert.ToBase64String(request.RowVersion)), snapshot.PreviousStatus,
                    snapshot.NewStatus, snapshot.EffectiveAt, snapshot.Reason,
                    decisions.Select(d => new EmployeeLifecycleDecision(d.Id, d.Decision, d.ActorUserId, d.ActorRoleCode, d.Reason, d.DecidedAt)).ToArray(), snapshot.LifecyclePolicyVersion);
            }, ct, auditTarget: _ => requestId.ToString());

    public Task<PagedResult<EmployeeLifecycleHistory>> HistoryAsync(Guid employeeId, EmployeeAssignmentListRequest input,
        AuthRequestContext context, CancellationToken ct) =>
        operation.ReadAsync([EmployeeLifecyclePermissions.Read], "hr.employee.lifecycle.history_read", employeeId,
            context, async (_, token) =>
            {
                OrganizationOperation.Validate(input);
                ValidatePage(input.Page, input.PageSize);
                await EmployeeAsync(employeeId, token);
                return await repository.HistoryAsync(employeeId, input, token);
            }, ct);

    private async Task<Employee> EmployeeAsync(Guid id, CancellationToken ct)
    {
        var employee = await hiring.FindAsync(id, ct) ?? throw EmployeeOperation.Missing();
        if (!await repository.HasApprovedEmploymentAsync(employee, ct)) throw EmployeeOperation.Conflict("employee_not_approved");
        return employee;
    }
    private async Task<ApprovalRequest> RequestAsync(Guid employeeId, Guid id, CancellationToken ct) =>
        await repository.FindRequestAsync(employeeId, id, ct) ?? throw EmployeeOperation.Missing();

    private async Task RequireUnprotectedTargetAsync(Guid employeeId, DateTimeOffset now, CancellationToken ct)
    {
        var account = await repository.FindAccountAsync(employeeId, ct);
        if (account is not null && await access.HasSystemRoleAsync(account.Id, now, includeScheduled: true, ct))
            throw EmployeeOperation.Conflict("protected_owner_requires_separate_handover");
    }
    private async Task<(EmployeeStatus Status, Guid? ChangeId, DateTimeOffset? EffectiveAt)> CurrentAsync(Employee employee, DateTimeOffset now, CancellationToken ct)
    {
        var current = await repository.CurrentChangesAsync(employee.Id, now, ct);
        if (current.Count > 1 && current[0].EffectiveAt == current[1].EffectiveAt)
            throw EmployeeOperation.Conflict("employee_status_history_conflict");
        return current.Count == 0 ? (employee.Status, null, null) : (current[0].NewStatus, current[0].Id, current[0].EffectiveAt);
    }
    private async Task<EmployeeLifecycleSnapshot> SnapshotAsync(ApprovalRequest request, CancellationToken ct)
    {
        try
        {
            var reference = JsonSerializer.Deserialize<PrivateFileReference>(request.SnapshotPrivateReference)
                ?? throw EmployeeOperation.Conflict("lifecycle_snapshot_unavailable");
            var snapshot = JsonSerializer.Deserialize<EmployeeLifecycleSnapshot>(await files.ReadAsync(reference, Purpose(request.SubjectId), ct));
            if (snapshot is null || snapshot.SchemaVersion is not (1 or 2) || snapshot.EmployeeId != request.SubjectId ||
                (snapshot.SchemaVersion == 1 && snapshot.LifecyclePolicyVersion is not null))
                throw EmployeeOperation.Conflict("lifecycle_snapshot_invalid");
            return snapshot;
        }
        catch (JsonException) { throw EmployeeOperation.Conflict("lifecycle_snapshot_invalid"); }
    }
    private static void ValidateReason(string? reason, bool required, int maximumLength = 2000)
    {
        if ((required && string.IsNullOrWhiteSpace(reason)) || reason?.Length > maximumLength || reason?.Any(char.IsControl) == true)
            throw EmployeeOperation.Invalid("invalid_lifecycle_reason");
    }
    private static void ValidatePage(int page, int pageSize)
    {
        if (page is < 1 or > 1000000 || pageSize is < 1 or > 100)
            throw EmployeeOperation.Invalid("invalid_lifecycle_page");
    }
    private static string Purpose(Guid employeeId) => EmployeeHiringService.Purpose(employeeId, "lifecycle");
    internal static EmployeeLifecycleHistory Map(EmployeeStatusChange change) => new(change.Id, change.PreviousStatus,
        change.NewStatus, change.EffectiveAt, change.Reason, change.ChangedByUserId, change.RecordedAt, change.ApprovalRequestId, change.LifecyclePolicyVersion);
}
