using System.Data;
using Cosmix.Application.Attendance.Common;
using Cosmix.Application.Attendance.Kpi;
using Cosmix.Domain.Common.Approvals;
using Cosmix.Domain.Common.Audit;
using Cosmix.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Cosmix.Infrastructure.Repositories.Attendance;

public sealed class KpiMissingEvidenceRepository(CosmixDbContext db) : IKpiMissingEvidenceRepository
{
    public void RequireTransaction()
    {
        if (db.Database.CurrentTransaction?.GetDbTransaction().IsolationLevel != IsolationLevel.Serializable)
            throw new InvalidOperationException("KPI missing-evidence persistence requires the actual outer serializable AttendanceOperation transaction.");
    }
    public Task<Guid?> AccountEmployeeAsync(Guid accountId, CancellationToken ct)
    {
        RequireTransaction();
        return db.UserAccounts.AsNoTracking().Where(x => x.Id == accountId).Select(x => (Guid?)x.EmployeeId).SingleOrDefaultAsync(ct);
    }
    public async Task<KpiMissingEvidenceRow?> FindAsync(Guid employee, Guid id, CancellationToken ct)
    {
        RequireTransaction();
        var request = await db.ApprovalRequests.SingleOrDefaultAsync(x => x.SubjectId == employee && x.Id == id && x.Stage == 1 &&
            x.RequestType.StartsWith(KpiMissingEvidenceRules.RequestPrefix), ct);
        if (request is null) return null;
        var decisions = await db.ApprovalDecisions.AsNoTracking().Where(x => x.ApprovalRequestId == id)
            .OrderBy(x => x.DecidedAt).ThenBy(x => x.Id).Take(2).ToArrayAsync(ct);
        return new(request, decisions);
    }
    internal IQueryable<ApprovalRequest> TargetQuery(Guid employee, DateOnly date, Guid leave)
    {
        var type = KpiMissingEvidenceRules.RequestType(date, leave);
        return db.ApprovalRequests.AsNoTracking().Where(x => x.SubjectId == employee && x.RequestType == type && x.Stage == 1);
    }
    public async Task<IReadOnlyList<ApprovalRequest>> TargetAsync(Guid employee, DateOnly date, Guid leave, CancellationToken ct)
    {
        RequireTransaction();
        var rows = await TargetQuery(employee, date, leave).OrderBy(x => x.RequestedAt).ThenBy(x => x.Id).Take(33).ToArrayAsync(ct);
        return rows.Length <= 32 ? rows : throw AttendanceOperation.Conflict("kpi_missing_evidence_target_limit");
    }
    internal IQueryable<ApprovalRequest> RangeQuery(Guid employee, DateOnly from, DateOnly to)
    {
        KpiMissingEvidenceRules.Range(employee, from, to);
        var lower = KpiMissingEvidenceRules.RequestPrefix + $"{from:yyyyMMdd}:";
        var upper = KpiMissingEvidenceRules.RequestPrefix + $"{to:yyyyMMdd}:";
        return db.ApprovalRequests.AsNoTracking().Where(x => x.SubjectId == employee && x.Stage == 1 &&
            x.RequestType.StartsWith(KpiMissingEvidenceRules.RequestPrefix) && x.RequestType.CompareTo(lower) >= 0 && x.RequestType.CompareTo(upper) < 0);
    }
    public async Task<IReadOnlyList<ApprovalRequest>> RangeAsync(Guid employee, DateOnly from, DateOnly to, CancellationToken ct)
    {
        RequireTransaction();
        var rows = await RangeQuery(employee, from, to).OrderBy(x => x.RequestType).ThenBy(x => x.Id).Take(513).ToArrayAsync(ct);
        return rows.Length <= 512 ? rows : throw AttendanceOperation.Conflict("kpi_missing_evidence_range_limit");
    }
    public async Task<IReadOnlyList<AuditEvent>> ProofAsync(Guid employee, Guid id, CancellationToken ct)
    {
        RequireTransaction(); var target = id.ToString();
        return await db.AuditEvents.AsNoTracking().Where(x => x.EmployeeScopeId == employee && x.TargetId == target &&
            x.TargetType == KpiMissingEvidenceRules.TargetType && x.EventType == KpiMissingEvidenceRules.ApprovedEvent)
            .OrderBy(x => x.OccurredAt).ThenBy(x => x.Id).Take(2).ToArrayAsync(ct);
    }
    internal IQueryable<AuditEvent> RevisionQuery(Guid employee, Guid id)
    {
        var target = id.ToString();
        return db.AuditEvents.AsNoTracking().Where(x => x.EmployeeScopeId == employee && x.TargetId == target &&
            x.TargetType == KpiMissingEvidenceRules.TargetType && x.EventType == KpiMissingEvidenceRules.RevisedEvent);
    }
    public async Task<IReadOnlyList<AuditEvent>> RevisionAuditsAsync(Guid employee, Guid id, CancellationToken ct)
    {
        RequireTransaction();
        var rows = await RevisionQuery(employee, id).OrderBy(x => x.OccurredAt).ThenBy(x => x.Id).Take(33).ToArrayAsync(ct);
        return rows.Length <= 32 ? rows : throw AttendanceOperation.Conflict("kpi_missing_evidence_revision_limit");
    }
    public void Add(ApprovalRequest request)
    {
        RequireTransaction();
        if (request.Stage != 1 || !request.RequestType.StartsWith(KpiMissingEvidenceRules.RequestPrefix, StringComparison.Ordinal))
            throw new InvalidOperationException("Only missing-evidence review requests can be staged here.");
        db.ApprovalRequests.Add(request);
    }
    public void Add(ApprovalDecision decision) { RequireTransaction(); db.ApprovalDecisions.Add(decision); }
    public void Add(AuditEvent audit)
    {
        RequireTransaction();
        if (audit.EventType is not (KpiMissingEvidenceRules.ApprovedEvent or KpiMissingEvidenceRules.RevisedEvent) || audit.TargetType != KpiMissingEvidenceRules.TargetType ||
            audit.EmployeeScopeId is null || audit.ProtectedChangesReference is null) throw new InvalidOperationException("A typed protected verified-event audit is required.");
        db.AuditEvents.Add(audit);
    }
    public void ExpectVersion(ApprovalRequest request, byte[] version)
    { RequireTransaction(); db.Entry(request).Property(x => x.RowVersion).OriginalValue = version; }
    public Task SaveChangesAsync(CancellationToken ct) { RequireTransaction(); return db.SaveChangesAsync(ct); }
}
