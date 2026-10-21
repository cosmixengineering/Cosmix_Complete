using Cosmix.Application.Attendance.Kpi;
using Cosmix.Domain.Common.Approvals;
using Cosmix.Domain.Common.Audit;
using Cosmix.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;

namespace Cosmix.Infrastructure.Repositories.Attendance;

public sealed class KpiRepository(CosmixDbContext db) : IKpiRepository
{
    public async Task<KpiRequestRow?> FindAsync(Guid employee, Guid id, CancellationToken ct)
    {
        var request = await db.ApprovalRequests.SingleOrDefaultAsync(x => x.Id == id && x.SubjectId == employee &&
            x.RequestType == AttendanceKpiService.RequestType, ct);
        if (request is null) return null;
        var decisions = await db.ApprovalDecisions.AsNoTracking().Where(x => x.ApprovalRequestId == id).OrderBy(x => x.Id).Take(2).ToArrayAsync(ct);
        return new(request, decisions);
    }
    public async Task<IReadOnlyList<AuditEvent>> FinalizationsAsync(Guid employee, DateOnly from, CancellationToken ct) =>
        await db.AuditEvents.AsNoTracking().Where(x => x.EventType == AttendanceKpiService.FinalizedEvent &&
            x.TargetType == AttendanceKpiService.TargetType && x.EmployeeScopeId == employee &&
            x.TargetId == AttendanceKpiService.Target(employee, from)).OrderBy(x => x.OccurredAt).ThenBy(x => x.Id).Take(129).ToArrayAsync(ct);
    public async Task<IReadOnlyList<ApprovalRequest>> PendingAsync(Guid employee, CancellationToken ct) =>
        await db.ApprovalRequests.AsNoTracking().Where(x => x.RequestType == AttendanceKpiService.RequestType &&
            x.SubjectId == employee && x.State == ApprovalState.Pending).OrderBy(x => x.RequestedAt).ThenBy(x => x.Id).Take(129).ToArrayAsync(ct);
    public void Add(ApprovalRequest request) => db.ApprovalRequests.Add(request);
    public void Add(ApprovalDecision decision) => db.ApprovalDecisions.Add(decision);
    public void Add(AuditEvent audit) => db.AuditEvents.Add(audit);
    public void ExpectVersion(ApprovalRequest request, byte[] version) => db.Entry(request).Property(x => x.RowVersion).OriginalValue = version;
    public Task SaveChangesAsync(CancellationToken ct) => db.SaveChangesAsync(ct);
}
