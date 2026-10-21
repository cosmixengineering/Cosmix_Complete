using Cosmix.Application.Attendance.Common;
using Cosmix.Application.HR.Leaves;
using Cosmix.Domain.Common.Approvals;
using Cosmix.Domain.Common.Audit;
using Cosmix.Domain.Authentication;
using Cosmix.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;

namespace Cosmix.Infrastructure.Repositories.HR;

public sealed class LeaveRecoveryWorkRepository(CosmixDbContext db) : ILeaveRecoveryWorkRepository
{
    public void RequireTransaction()
    {
        if (db.Database.CurrentTransaction is null) throw new InvalidOperationException("Recovery work requires the authorized serializable transaction.");
    }
    public async Task<LeaveRecoveryWorkRow?> FindAsync(Guid employeeId, Guid id, CancellationToken ct)
    {
        RequireTransaction();
        var target = id.ToString();
        if (!await db.AuditEvents.AnyAsync(x => x.EmployeeScopeId == employeeId && x.TargetType == LeaveRecoveryWorkEvidence.FactTarget &&
            x.EventType == LeaveRecoveryWorkEvidence.Submitted && x.TargetId == target, ct)) return null;
        var approvals = await db.ApprovalRequests.Where(x => x.SubjectId == id && x.RequestType == LeaveRecoveryWorkEvidence.RequestType).Take(2).ToArrayAsync(ct);
        if (approvals.Length != 1) throw AttendanceOperation.Conflict("leave_recovery_work_approval_unavailable");
        var approval = approvals[0];
        var decisions = await db.ApprovalDecisions.AsNoTracking().Where(x => x.ApprovalRequestId == approval.Id).Take(2).ToArrayAsync(ct);
        if (decisions.Length > 1) throw AttendanceOperation.Conflict("leave_recovery_work_decision_unavailable");
        return new(approval, decisions.SingleOrDefault());
    }
    public async Task<IReadOnlyList<AuditEvent>> FactsAsync(Guid employeeId, string operation, CancellationToken ct)
    {
        RequireTransaction();
        var rows = await db.AuditEvents.AsNoTracking().Where(x => x.EmployeeScopeId == employeeId &&
            x.TargetType == LeaveRecoveryWorkEvidence.FactTarget && x.EventType == operation)
            .OrderBy(x => x.OccurredAt).ThenBy(x => x.Id).Take(1001).ToArrayAsync(ct);
        if (rows.Length > 1000) throw AttendanceOperation.Conflict("leave_recovery_work_history_limit");
        return rows;
    }
    public async Task<IReadOnlyList<(Guid EmployeeId, Guid Id)>> PendingAsync(CancellationToken ct)
    {
        RequireTransaction();
        var rows = await (from fact in db.AuditEvents.AsNoTracking()
            join approval in db.ApprovalRequests.AsNoTracking() on fact.TargetId equals approval.SubjectId.ToString()
            where fact.TargetType == LeaveRecoveryWorkEvidence.FactTarget && fact.EventType == LeaveRecoveryWorkEvidence.Submitted &&
                approval.RequestType == LeaveRecoveryWorkEvidence.RequestType && approval.State == ApprovalState.Pending
            orderby approval.RequestedAt, approval.Id
            select new { fact.EmployeeScopeId, Id = approval.SubjectId }).Take(201).ToArrayAsync(ct);
        if (rows.Length > 200 || rows.Any(x => !x.EmployeeScopeId.HasValue) || rows.Select(x => x.Id).Distinct().Count() != rows.Length)
            throw AttendanceOperation.Conflict("leave_recovery_work_queue_unavailable");
        return rows.Select(x => (x.EmployeeScopeId!.Value, x.Id)).ToArray();
    }
    public async Task<IReadOnlyList<Guid>> ReviewRecipientAccountIdsAsync(Guid employeeId, Guid requesterAccountId, DateTimeOffset now, CancellationToken ct)
    {
        RequireTransaction();
        var rows = await (from assignment in db.UserRoleAssignments.AsNoTracking()
            join permission in db.RolePermissions.AsNoTracking() on assignment.RoleId equals permission.RoleId
            join scope in db.UserAccessScopes.AsNoTracking() on assignment.Id equals scope.UserRoleAssignmentId
            where assignment.UserAccountId != requesterAccountId && assignment.UserAccount.EmployeeId != employeeId &&
                assignment.UserAccount.Status == AccountStatus.Active && assignment.UserAccount.WebAccessEnabled && assignment.Role.IsActive &&
                (assignment.Role.SystemRole == SystemRoleKind.Admin || assignment.Role.SystemRole == SystemRoleKind.SuperAdmin) &&
                assignment.EffectiveFrom <= now && (assignment.EffectiveTo == null || assignment.EffectiveTo > now) &&
                (assignment.RevokedAt == null || assignment.RevokedAt > now) && permission.GrantedAt <= now && permission.RevokedAt == null &&
                permission.Permission.Code == LeavePermissions.Review && scope.Kind == AccessScopeKind.All
            select assignment.UserAccountId).Distinct().OrderBy(x => x).Take(257).ToArrayAsync(ct);
        if (rows.Length > 256) throw AttendanceOperation.Conflict("leave_recovery_review_recipient_limit");
        return rows;
    }
    public void Add(ApprovalRequest request) { RequireTransaction(); db.ApprovalRequests.Add(request); }
    public void Add(ApprovalDecision decision) { RequireTransaction(); db.ApprovalDecisions.Add(decision); }
    public void Add(AuditEvent fact) { RequireTransaction(); db.AuditEvents.Add(fact); }
    public void ExpectVersion(ApprovalRequest request, byte[] version)
    { RequireTransaction(); db.Entry(request).Property(x => x.RowVersion).OriginalValue = version; }
    public Task SaveAsync(CancellationToken ct) { RequireTransaction(); return db.SaveChangesAsync(ct); }
}
