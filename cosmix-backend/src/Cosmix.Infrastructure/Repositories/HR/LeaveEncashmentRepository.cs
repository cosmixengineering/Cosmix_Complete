using Cosmix.Application.HR.Leaves;
using Cosmix.Domain.Common.Approvals;
using Cosmix.Domain.Common.Audit;
using Cosmix.Domain.Common.Idempotency;
using Cosmix.Domain.HR.Leaves;
using Cosmix.Application.Attendance.Common;
using Cosmix.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;

namespace Cosmix.Infrastructure.Repositories.HR;

public sealed class LeaveEncashmentRepository(CosmixDbContext db) : ILeaveEncashmentRepository
{
    public async Task<IReadOnlyList<ApprovalDecision>> DecisionsAsync(Guid approvalId, CancellationToken ct) =>
        await db.Set<ApprovalDecision>().Where(x => x.ApprovalRequestId == approvalId)
            .OrderBy(x => x.DecidedAt).ThenBy(x => x.Id).Take(2).ToArrayAsync(ct);
    public Task<LeaveEncashmentRequest?> FindAsync(Guid id, Guid? employeeId, CancellationToken ct) =>
        db.LeaveEncashmentRequests
            .Include(x => x.ApprovalRequest)
            .Include(x => x.PolicyVersion)
            .Where(x => x.Id == id && (!employeeId.HasValue || x.EmployeeId == employeeId.Value))
            .SingleOrDefaultAsync(ct);

    public async Task<IReadOnlyList<LeaveEncashmentRequest>> ListAsync(Guid? employeeId, CancellationToken ct) =>
        await db.LeaveEncashmentRequests
            .Include(x => x.ApprovalRequest)
            .Include(x => x.PolicyVersion)
            .Where(x => !employeeId.HasValue || x.EmployeeId == employeeId.Value)
            .OrderByDescending(x => x.CreatedAt)
            .Take(200)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<LeaveLedgerEntry>> LedgerAsync(Guid employeeId, DateOnly from, DateOnly to, CancellationToken ct)
    {
        var entries = await db.LeaveLedgerEntries.Where(x => x.EmployeeId == employeeId && x.LeaveYearStart == from && x.LeaveYearEnd == to)
            .OrderBy(x => x.RecordedAt).ThenBy(x => x.Id).Take(10001).ToArrayAsync(ct);
        if (entries.Length > 10000) throw AttendanceOperation.Conflict("leave_ledger_history_limit");
        return entries;
    }

    public void Add(LeaveEncashmentRequest request, ApprovalRequest approval)
    {
        db.LeaveEncashmentRequests.Add(request);
        db.ApprovalRequests.Add(approval);
    }

    public async Task<IReadOnlyList<LeaveRecoveryCommitment>> GetRecoveryCommitmentsAsync(
        Guid leaveRequestId,
        CancellationToken ct)
    {
        var rows = await db.LeaveRecoveryCommitments
            .Where(c => c.LeaveRequestId == leaveRequestId)
            .OrderBy(c => c.CommitmentDate)
            .Take(1001).ToListAsync(ct);
        if (rows.Count > 1000) throw AttendanceOperation.Conflict("leave_recovery_history_limit");
        return rows;
    }

    public void Add(LeaveRecoveryCommitment commitment) => db.LeaveRecoveryCommitments.Add(commitment);

    public async Task<AuditEvent?> RecoveryAuditAsync(Guid commitmentId, CancellationToken ct)
    {
        var target = commitmentId.ToString();
        var rows = await db.AuditEvents.AsNoTracking().Where(x => x.TargetType == "Attendance" &&
            x.TargetId == target && x.EventType == LeaveEncashmentService.RecoveryOperation).Take(2).ToArrayAsync(ct);
        if (rows.Length > 1) throw AttendanceOperation.Conflict("leave_recovery_audit_conflict");
        return rows.SingleOrDefault();
    }

    public Task<IdempotencyReceipt?> RecoveryReceiptAsync(Guid actorId, string key, CancellationToken ct)
    {
        var actor = actorId.ToString();
        return db.Set<IdempotencyReceipt>().AsNoTracking().SingleOrDefaultAsync(x => x.ActorUserId == actor &&
            x.Operation == LeaveEncashmentService.RecoveryOperation && x.Key == key, ct);
    }

    public async Task<IReadOnlyList<LeaveRecoveryCommitment>> RecoveryOnDateAsync(Guid employeeId, DateOnly date, CancellationToken ct)
    {
        var rows = await db.LeaveRecoveryCommitments.Where(x => x.LeaveRequest.EmployeeId == employeeId && x.CommitmentDate == date)
            .Take(1001).ToArrayAsync(ct);
        if (rows.Length > 1000) throw AttendanceOperation.Conflict("leave_recovery_history_limit");
        return rows;
    }

    public Task SaveChangesAsync(CancellationToken ct) => db.SaveChangesAsync(ct);
}
