using Cosmix.Application.Common.Models;
using Cosmix.Application.HR.Employees;
using Cosmix.Domain.Authentication;
using Cosmix.Domain.Common.Approvals;
using Cosmix.Domain.HR.Employees;
using Cosmix.Domain.HR.Onboarding;
using Cosmix.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;

namespace Cosmix.Infrastructure.Repositories.HR;

public sealed class EmployeeLifecycleRepository(CosmixDbContext db) : IEmployeeLifecycleRepository
{
    public async Task<bool> HasApprovedEmploymentAsync(Employee employee, CancellationToken ct) =>
        employee.OnboardingStatus == OnboardingStatus.Approved && employee.ApprovedOnboardingDecisionId.HasValue &&
        await db.EmployeeOnboardingDecisions.AnyAsync(d => d.Id == employee.ApprovedOnboardingDecisionId &&
            d.EmployeeId == employee.Id && d.Decision == OnboardingDecision.Approve &&
            d.Submission.EmployeeId == employee.Id && d.Submission.Version == employee.SubmittedVersion &&
            d.Submission.Provenance == d.Provenance, ct);

    public Task<UserAccount?> FindAccountAsync(Guid employeeId, CancellationToken ct) =>
        db.UserAccounts.SingleOrDefaultAsync(x => x.EmployeeId == employeeId, ct);

    public async Task<IReadOnlyList<EmployeeStatusChange>> CurrentChangesAsync(Guid employeeId,
        DateTimeOffset now, CancellationToken ct) =>
        await db.EmployeeStatusChanges.AsNoTracking().Where(x => x.EmployeeId == employeeId && x.EffectiveAt <= now)
            .OrderByDescending(x => x.EffectiveAt).ThenByDescending(x => x.RecordedAt).ThenBy(x => x.Id)
            .Take(2).ToArrayAsync(ct);

    public async Task<IReadOnlyList<EmployeeStatusChange>> UnappliedChangesAsync(Guid employeeId, Guid? appliedChangeId,
        DateTimeOffset now, CancellationToken ct)
    {
        var changes = db.EmployeeStatusChanges.AsNoTracking().Where(x => x.EmployeeId == employeeId && x.EffectiveAt <= now);
        if (appliedChangeId.HasValue)
        {
            var applied = await db.EmployeeStatusChanges.AsNoTracking().SingleOrDefaultAsync(x => x.Id == appliedChangeId && x.EmployeeId == employeeId, ct)
                ?? throw EmployeeOperation.Conflict("employee_status_applied_marker_invalid");
            changes = changes.Where(x => x.EffectiveAt > applied.EffectiveAt);
        }
        var rows = await changes.OrderBy(x => x.EffectiveAt).ThenBy(x => x.RecordedAt).ThenBy(x => x.Id).Take(101).ToArrayAsync(ct);
        if (rows.Length > 100) throw EmployeeOperation.Conflict("employee_status_history_requires_review");
        return rows;
    }

    public async Task<IReadOnlyList<RegisteredDevice>> UnrevokedDevicesAsync(Guid accountId, CancellationToken ct) =>
        await db.RegisteredDevices.Where(x => x.UserAccountId == accountId && x.RevokedAt == null).ToArrayAsync(ct);

    public async Task<IReadOnlyList<UserRoleAssignment>> UnrevokedRoleAssignmentsAsync(Guid accountId, CancellationToken ct) =>
        await db.UserRoleAssignments.Where(x => x.UserAccountId == accountId && x.RevokedAt == null).ToArrayAsync(ct);

    public async Task RevokeFaceEnrollmentsAsync(Guid employeeId, Guid statusChangeId, DateTimeOffset now, CancellationToken ct)
    {
        foreach (var enrollment in await db.FaceEnrollments.Where(x => x.EmployeeId == employeeId && x.IsActive).ToArrayAsync(ct))
            enrollment.Revoke("system:employee-lifecycle", $"employee-lifecycle:{statusChangeId:D}", now);
    }

    public Task<bool> HasFutureChangeAsync(Guid employeeId, DateTimeOffset now, CancellationToken ct) =>
        db.EmployeeStatusChanges.AnyAsync(x => x.EmployeeId == employeeId && x.EffectiveAt > now, ct);

    public Task<EmployeeStatusChange?> NextChangeAsync(Guid employeeId, DateTimeOffset now, CancellationToken ct) =>
        db.EmployeeStatusChanges.AsNoTracking().Where(x => x.EmployeeId == employeeId && x.EffectiveAt > now)
            .OrderBy(x => x.EffectiveAt).ThenByDescending(x => x.RecordedAt).ThenBy(x => x.Id)
            .FirstOrDefaultAsync(ct);

    public Task<bool> HasPendingRequestAsync(Guid employeeId, CancellationToken ct) =>
        db.ApprovalRequests.AnyAsync(x => x.RequestType == EmployeeLifecycleService.RequestType &&
            x.SubjectId == employeeId && x.State == ApprovalState.Pending, ct);

    public Task<ApprovalRequest?> FindRequestAsync(Guid employeeId, Guid requestId, CancellationToken ct) =>
        db.ApprovalRequests.SingleOrDefaultAsync(x => x.RequestType == EmployeeLifecycleService.RequestType &&
            x.SubjectId == employeeId && x.Id == requestId, ct);

    public async Task<IReadOnlyList<ApprovalDecision>> DecisionsAsync(Guid requestId, CancellationToken ct) =>
        await db.ApprovalDecisions.AsNoTracking().Where(x => x.ApprovalRequestId == requestId &&
                x.Request.RequestType == EmployeeLifecycleService.RequestType)
            .OrderBy(x => x.DecidedAt).ThenBy(x => x.Id).ToArrayAsync(ct);

    public async Task<PagedResult<ApprovalRequest>> OwnRequestsAsync(Guid employeeId, string requesterAccountId,
        int page, int pageSize, CancellationToken ct)
    {
        ValidatePage(page, pageSize);
        var query = db.ApprovalRequests.AsNoTracking().Where(x =>
            x.RequestType == EmployeeLifecycleService.RequestType && x.SubjectId == employeeId &&
            x.RequesterUserId == requesterAccountId);
        var count = await query.CountAsync(ct);
        var rows = await query.OrderByDescending(x => x.RequestedAt).ThenBy(x => x.Id)
            .Skip((page - 1) * pageSize).Take(pageSize).ToArrayAsync(ct);
        return new(rows, count, page, pageSize);
    }

    public async Task<PagedResult<EmployeeLifecycleRequestSummary>> RequestsAsync(Guid? employeeId,
        EmployeeLifecycleQuery query, CancellationToken ct)
    {
        ValidatePage(query.Page, query.PageSize);
        var requests = db.ApprovalRequests.AsNoTracking()
            .Where(x => x.RequestType == EmployeeLifecycleService.RequestType);
        if (employeeId.HasValue) requests = requests.Where(x => x.SubjectId == employeeId.Value);
        if (query.State.HasValue) requests = requests.Where(x => x.State == query.State.Value);
        var count = await requests.CountAsync(ct);
        var rows = await requests.OrderByDescending(x => x.RequestedAt).ThenBy(x => x.Id)
            .Skip((query.Page - 1) * query.PageSize).Take(query.PageSize)
            .Select(x => new { x.Id, x.SubjectId, x.State, x.RequesterUserId, x.RequestedAt, x.ClosedAt, x.RowVersion })
            .ToArrayAsync(ct);
        return new(rows.Select(x => new EmployeeLifecycleRequestSummary(x.Id, x.SubjectId, x.State,
            x.RequesterUserId, x.RequestedAt, x.ClosedAt, Convert.ToBase64String(x.RowVersion))).ToArray(),
            count, query.Page, query.PageSize);
    }

    public async Task<PagedResult<EmployeeLifecycleHistory>> HistoryAsync(Guid employeeId,
        EmployeeAssignmentListRequest query, CancellationToken ct)
    {
        ValidatePage(query.Page, query.PageSize);
        var changes = db.EmployeeStatusChanges.AsNoTracking().Where(x => x.EmployeeId == employeeId);
        var count = await changes.CountAsync(ct);
        var rows = await changes.OrderByDescending(x => x.EffectiveAt).ThenByDescending(x => x.RecordedAt).ThenBy(x => x.Id)
            .Skip((query.Page - 1) * query.PageSize).Take(query.PageSize)
            .Select(x => new EmployeeLifecycleHistory(x.Id, x.PreviousStatus, x.NewStatus, x.EffectiveAt,
                x.Reason, x.ChangedByUserId, x.RecordedAt, x.ApprovalRequestId, x.LifecyclePolicyVersion)).ToArrayAsync(ct);
        return new(rows, count, query.Page, query.PageSize);
    }

    public async Task<PagedResult<EmployeeArchiveHistory>> ArchiveHistoryAsync(Guid employeeId,
        EmployeeAssignmentListRequest query, CancellationToken ct)
    {
        ValidatePage(query.Page, query.PageSize);
        var changes = db.Set<EmployeeArchiveChange>().AsNoTracking().Where(x => x.EmployeeId == employeeId);
        var count = await changes.CountAsync(ct);
        var rows = await changes.OrderByDescending(x => x.RecordedAt).ThenBy(x => x.Id)
            .Skip((query.Page - 1) * query.PageSize).Take(query.PageSize)
            .Select(x => new EmployeeArchiveHistory(x.Id, x.PreviousArchived, x.IsArchived, x.EmploymentStatus,
                x.Reason, x.ActorUserId, x.ActorRoleCode, x.RecordedAt)).ToArrayAsync(ct);
        return new(rows, count, query.Page, query.PageSize);
    }

    public async Task<IReadOnlyList<Guid>> DueEmployeesAsync(DateTimeOffset now, int limit, CancellationToken ct, long afterEmployeeNumber = 0)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(limit);
        ArgumentOutOfRangeException.ThrowIfNegative(afterEmployeeNumber);
        var batchSize = Math.Min(limit, 100);
        // Selecting employees keeps IDs unique; the latest-row subquery also includes legacy history.
        return await db.Employees.AsNoTracking().Where(employee => employee.EmployeeNumber > afterEmployeeNumber && db.EmployeeStatusChanges.Any(change =>
                change.EmployeeId == employee.Id && change.EffectiveAt <= now &&
                employee.LastAppliedStatusChangeId != change.Id && change.ApprovalRequestId != null &&
                db.ApprovalRequests.Any(request => request.Id == change.ApprovalRequestId &&
                    request.RequestType == EmployeeLifecycleService.RequestType && request.SubjectId == employee.Id &&
                    request.State == ApprovalState.Approved) &&
                change.Id == db.EmployeeStatusChanges.Where(latest => latest.EmployeeId == employee.Id && latest.EffectiveAt <= now)
                    .OrderByDescending(latest => latest.EffectiveAt).ThenByDescending(latest => latest.RecordedAt)
                    .ThenBy(latest => latest.Id).Select(latest => latest.Id).FirstOrDefault()))
            .OrderBy(employee => employee.EmployeeNumber).Select(employee => employee.Id).Take(batchSize).ToArrayAsync(ct);
    }

    public void ExpectVersion(ApprovalRequest request, byte[] version)
    {
        if (!request.RowVersion.SequenceEqual(version)) throw EmployeeOperation.Conflict("lifecycle_request_version_conflict");
        db.Entry(request).Property(x => x.RowVersion).OriginalValue = version;
    }

    public void AddRequest(ApprovalRequest request) => db.ApprovalRequests.Add(request);
    public void AddDecision(ApprovalDecision decision) => db.ApprovalDecisions.Add(decision);
    public void AddStatusChange(EmployeeStatusChange change) => db.EmployeeStatusChanges.Add(change);
    public void AddArchiveChange(EmployeeArchiveChange change) => db.Set<EmployeeArchiveChange>().Add(change);

    private static void ValidatePage(int page, int pageSize)
    {
        if (page is < 1 or > 1000000 || pageSize is < 1 or > 100)
            throw EmployeeOperation.Invalid("invalid_lifecycle_pagination");
    }
}
