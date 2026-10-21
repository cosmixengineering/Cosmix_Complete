using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using System.Text.Json.Serialization;
using Cosmix.Application.Attendance.Common;
using Cosmix.Application.Attendance.Rosters;
using Cosmix.Application.Authentication;
using Cosmix.Application.Authentication.Access;
using Cosmix.Application.Common.Files;
using Cosmix.Application.Common.Models;
using Cosmix.Application.HR.Employees;
using Cosmix.Application.HR.Organization;
using Cosmix.Domain.Common.Approvals;
using Cosmix.Domain.HR.Employees;

namespace Cosmix.Application.HR.EmployeeSelfService;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record SubmitMyResignationRequest(
    [Required, StringLength(12, MinimumLength = 12)] string ExpectedVersion,
    DateTimeOffset RequestedEffectiveAt,
    [Required, StringLength(1000)] string Reason);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record CancelMyResignationRequest(
    [Required, StringLength(12, MinimumLength = 12)] string ExpectedVersion,
    [Required, StringLength(1000)] string Reason);

public sealed record MyEmploymentRequestResponse(Guid Id, string State, string Version,
    string RequestedStatus, DateTimeOffset RequestedAt, DateTimeOffset? RequestedEffectiveAt, string Reason);
public sealed record MyResignationContext(DateTimeOffset ServerTimeUtc, string TimeZone,
    string EmployeeVersion, string CurrentStatus, bool CanSubmit, string? BlockedReason,
    PagedResult<MyEmploymentRequestResponse> Requests);

/// <summary>Self-only intake into the existing lifecycle approval queue. This never applies a
/// status change, revokes access, approves a request or exposes another requester's proposal.</summary>
public sealed class EmployeeResignationService(EmployeeResignationOperation operation, IEmployeeHiringRepository hiring,
    IEmployeeLifecycleRepository lifecycle, IAccessManagementRepository access, IPrivateFileStore files,
    EmployeeLifecyclePolicy policy)
{
    public Task<MyResignationContext> ContextAsync(int page, int pageSize, AuthRequestContext context, CancellationToken ct) =>
        operation.ReadAsync("ess.resignation.read", context,
            async (actor, token) =>
            {
                if (page is < 1 or > 1000000 || pageSize is < 1 or > 50) throw EmployeeOperation.Invalid("invalid_lifecycle_page");
                var employee = await EmployeeAsync(actor.Identity.EmployeeId, token);
                var current = await CurrentAsync(employee, actor.Now, token);
                var blocked = await BlockedAsync(employee, current.Status, actor.Now, token);
                var rows = await lifecycle.OwnRequestsAsync(employee.Id, actor.Identity.AccountId.ToString(), page, pageSize, token);
                var requests = new List<MyEmploymentRequestResponse>();
                foreach (var request in rows.Items) requests.Add(await MapAsync(request, token));
                return new MyResignationContext(actor.Now, RosterSchedule.TimeZoneId,
                    Convert.ToBase64String(employee.RowVersion), current.Status.ToString(), blocked is null, blocked,
                    new(requests, rows.TotalCount, rows.Page, rows.PageSize));
            }, ct);

    public Task<MyEmploymentRequestResponse> SubmitAsync(SubmitMyResignationRequest input, string key,
        AuthRequestContext context, CancellationToken ct) =>
        operation.WriteAsync("ess.resignation.submit",
            key, input, input.Reason, context, async (actor, token) =>
            {
                OrganizationOperation.Validate(input);
                ValidateReason(input.Reason);
                if (input.RequestedEffectiveAt <= actor.Now) throw EmployeeOperation.Invalid("resignation_effective_time_must_be_future");
                var employee = await EmployeeAsync(actor.Identity.EmployeeId, token);
                hiring.ExpectVersion(employee, OrganizationOperation.Version(input.ExpectedVersion));
                var current = await CurrentAsync(employee, actor.Now, token);
                var blocked = await BlockedAsync(employee, current.Status, actor.Now, token);
                if (blocked is not null) throw EmployeeOperation.Conflict(blocked);
                var version = await policy.ValidateTransitionAsync(current.Status, EmployeeStatus.Resigned,
                    actor.Now, input.RequestedEffectiveAt, token);
                var snapshot = new EmployeeLifecycleSnapshot(2, employee.Id, current.Status, current.ChangeId,
                    EmployeeStatus.Resigned, input.RequestedEffectiveAt.ToUniversalTime(), input.Reason.Trim(), version);
                var evidence = await files.WriteAsync(JsonSerializer.SerializeToUtf8Bytes(snapshot), Purpose(employee.Id), token);
                var request = new ApprovalRequest(EmployeeLifecycleService.RequestType, employee.Id,
                    actor.Identity.AccountId.ToString(), JsonSerializer.Serialize(evidence), 1, actor.Now);
                lifecycle.AddRequest(request);
                await hiring.SaveChangesAsync(token);
                var result = await MapAsync(request, token);
                return new AttendanceChange<MyEmploymentRequestResponse>(request.Id, null, result);
            }, ct);

    public Task<MyEmploymentRequestResponse> CancelAsync(Guid id, CancelMyResignationRequest input, string key,
        AuthRequestContext context, CancellationToken ct) =>
        operation.WriteAsync("ess.resignation.cancel",
            key, new { id, input }, input.Reason, context, async (actor, token) =>
            {
                OrganizationOperation.Validate(input);
                ValidateReason(input.Reason);
                var request = await lifecycle.FindRequestAsync(actor.Identity.EmployeeId, id, token) ?? throw EmployeeOperation.Missing();
                if (request.RequesterUserId != actor.Identity.AccountId.ToString()) throw EmployeeOperation.Missing();
                var snapshot = await SnapshotAsync(request, token);
                if (snapshot.NewStatus != EmployeeStatus.Resigned) throw EmployeeOperation.Denied();
                lifecycle.ExpectVersion(request, OrganizationOperation.Version(input.ExpectedVersion));
                if (request.State != ApprovalState.Pending) throw EmployeeOperation.Conflict("lifecycle_request_already_closed");
                request.Cancel(actor.Identity.AccountId.ToString(), actor.Now);
                await hiring.SaveChangesAsync(token);
                return new AttendanceChange<MyEmploymentRequestResponse>(request.Id, null, await MapAsync(request, token));
            }, ct);

    private async Task<Employee> EmployeeAsync(Guid id, CancellationToken ct)
    {
        var employee = await hiring.FindAsync(id, ct) ?? throw EmployeeOperation.Missing();
        if (!await lifecycle.HasApprovedEmploymentAsync(employee, ct)) throw EmployeeOperation.Conflict("employee_not_approved");
        return employee;
    }

    private async Task<string?> BlockedAsync(Employee employee, EmployeeStatus current, DateTimeOffset now, CancellationToken ct)
    {
        if (employee.IsArchived || current != EmployeeStatus.Active) return "resignation_requires_active_employment";
        var account = await lifecycle.FindAccountAsync(employee.Id, ct);
        if (account is not null && await access.HasSystemRoleAsync(account.Id, now, includeScheduled: true, ct))
            return "protected_owner_requires_separate_handover";
        if (await lifecycle.HasPendingRequestAsync(employee.Id, ct) || await lifecycle.HasFutureChangeAsync(employee.Id, now, ct))
            return "employee_lifecycle_request_or_change_outstanding";
        return null;
    }

    private async Task<(EmployeeStatus Status, Guid? ChangeId)> CurrentAsync(Employee employee, DateTimeOffset now, CancellationToken ct)
    {
        var changes = await lifecycle.CurrentChangesAsync(employee.Id, now, ct);
        if (changes.Count > 1 && changes[0].EffectiveAt == changes[1].EffectiveAt)
            throw EmployeeOperation.Conflict("employee_status_history_conflict");
        return changes.Count == 0 ? (employee.Status, null) : (changes[0].NewStatus, changes[0].Id);
    }

    private async Task<EmployeeLifecycleSnapshot> SnapshotAsync(ApprovalRequest request, CancellationToken ct)
    {
        try
        {
            var reference = JsonSerializer.Deserialize<PrivateFileReference>(request.SnapshotPrivateReference)
                ?? throw EmployeeOperation.Conflict("lifecycle_snapshot_unavailable");
            var snapshot = JsonSerializer.Deserialize<EmployeeLifecycleSnapshot>(await files.ReadAsync(reference, Purpose(request.SubjectId), ct));
            if (snapshot is null || snapshot.SchemaVersion is not (1 or 2) || snapshot.EmployeeId != request.SubjectId)
                throw EmployeeOperation.Conflict("lifecycle_snapshot_invalid");
            return snapshot;
        }
        catch (JsonException) { throw EmployeeOperation.Conflict("lifecycle_snapshot_invalid"); }
    }

    private async Task<MyEmploymentRequestResponse> MapAsync(ApprovalRequest request, CancellationToken ct)
    {
        var snapshot = await SnapshotAsync(request, ct);
        return new(request.Id, request.State.ToString(), Convert.ToBase64String(request.RowVersion),
            snapshot.NewStatus.ToString(), request.RequestedAt, snapshot.EffectiveAt, snapshot.Reason);
    }

    private static void ValidateReason(string reason)
    {
        if (string.IsNullOrWhiteSpace(reason) || reason.Length > 1000 || reason.Any(char.IsControl))
            throw EmployeeOperation.Invalid("invalid_lifecycle_reason");
    }
    private static string Purpose(Guid employeeId) => EmployeeHiringService.Purpose(employeeId, "lifecycle");
}
