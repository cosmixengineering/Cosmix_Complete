using Cosmix.Application.Attendance.Common;
using Cosmix.Application.Attendance.Kpi;
using Cosmix.Application.Common.Models;
using Cosmix.Domain.Common.Approvals;
using Cosmix.Domain.Common.Audit;
using Cosmix.Domain.HR.EmployeeDocuments;
using Cosmix.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;

namespace Cosmix.Infrastructure.Repositories.Attendance;

/// <summary>Uses only existing approvals, decisions, audits and employee document registrations.</summary>
public sealed class KpiDisputeRepository(CosmixDbContext db) : IKpiDisputeRepository
{
    public void RequireTransaction()
    {
        if (db.Database.CurrentTransaction is null) throw new InvalidOperationException("KPI dispute persistence requires the existing authorized transaction.");
    }
    public async Task<KpiDisputeRow?> FindAsync(Guid employeeId, Guid requestId, CancellationToken ct)
    {
        RequireTransaction();
        if (employeeId == Guid.Empty || requestId == Guid.Empty) throw AttendanceOperation.Invalid();
        var row = await db.ApprovalRequests.SingleOrDefaultAsync(x => x.Id == requestId && x.SubjectId == employeeId &&
            x.Stage == 1 && x.RequestType.StartsWith(KpiDisputeRules.RequestPrefix), ct);
        if (row is null) return null;
        var decisions = await db.ApprovalDecisions.AsNoTracking().Where(x => x.ApprovalRequestId == row.Id)
            .OrderBy(x => x.DecidedAt).ThenBy(x => x.Id).Take(2).ToArrayAsync(ct);
        return new(row, decisions);
    }
    private IQueryable<ApprovalRequest> YearQuery(Guid employee, DateOnly from, Guid? requester, Guid? excludeRequester)
    {
        RequireTransaction();
        KpiDisputeRules.Year(from);
        if (employee == Guid.Empty || requester == Guid.Empty || excludeRequester == Guid.Empty ||
            requester.HasValue && excludeRequester.HasValue) throw AttendanceOperation.Invalid();
        var prefix = KpiDisputeRules.YearPrefix(from);
        var query = db.ApprovalRequests.AsNoTracking().Where(x => x.SubjectId == employee && x.Stage == 1 && x.RequestType.StartsWith(prefix));
        if (requester is Guid owner) { var account = owner.ToString(); query = query.Where(x => x.RequesterUserId == account); }
        if (excludeRequester is Guid excluded) { var account = excluded.ToString(); query = query.Where(x => x.RequesterUserId != account); }
        return query;
    }
    public async Task<IReadOnlyList<ApprovalRequest>> YearAsync(Guid employeeId, DateOnly yearFrom, Guid? requester,
        Guid? excludeRequester, CancellationToken ct)
    {
        var rows = await YearQuery(employeeId, yearFrom, requester, excludeRequester).OrderBy(x => x.RequestedAt).ThenBy(x => x.Id)
            .Take(KpiDisputeRules.YearLimit + 1).ToArrayAsync(ct);
        if (rows.Length > KpiDisputeRules.YearLimit) throw AttendanceOperation.Conflict("kpi_dispute_year_limit");
        return rows;
    }
    public async Task<PagedResult<ApprovalRequest>> PageAsync(Guid employeeId, KpiDisputeListRequest request,
        Guid? requester, Guid? excludeRequester, CancellationToken ct)
    {
        KpiDisputeRules.List(request);
        var query = YearQuery(employeeId, request.YearFrom, requester, excludeRequester);
        var count = await query.Take(KpiDisputeRules.YearLimit + 1).CountAsync(ct);
        if (count > KpiDisputeRules.YearLimit) throw AttendanceOperation.Conflict("kpi_dispute_year_limit");
        var rows = await query.OrderByDescending(x => x.RequestedAt).ThenBy(x => x.Id)
            .Skip(checked((request.Page - 1) * request.PageSize)).Take(request.PageSize).ToArrayAsync(ct);
        return new(rows, count, request.Page, request.PageSize);
    }
    public async Task<IReadOnlyList<AuditEvent>> EvidenceAsync(Guid employeeId, Guid requestId, string eventType, CancellationToken ct)
    {
        RequireTransaction();
        if (employeeId == Guid.Empty || requestId == Guid.Empty ||
            eventType is not (KpiDisputeRules.SubmittedEvent or KpiDisputeRules.AmendmentEvent)) throw AttendanceOperation.Invalid();
        var target = requestId.ToString();
        return await db.AuditEvents.AsNoTracking().Where(x => x.EmployeeScopeId == employeeId &&
            x.TargetType == KpiDisputeRules.TargetType && x.TargetId == target && x.EventType == eventType)
            .OrderBy(x => x.OccurredAt).ThenBy(x => x.Id).Take(2).ToArrayAsync(ct);
    }
    public async Task<IReadOnlyList<EmployeeDocument>> DocumentsAsync(Guid employeeId, IReadOnlyList<Guid> ids, CancellationToken ct)
    {
        RequireTransaction();
        KpiDisputeRules.Documents(ids);
        if (employeeId == Guid.Empty) throw AttendanceOperation.Invalid();
        return await db.EmployeeDocuments.AsNoTracking().Where(x => x.EmployeeId == employeeId && ids.Contains(x.Id) &&
            x.Privacy == EmployeeDocumentPrivacy.Standard && x.DocumentType == EmployeeDocumentType.Other)
            .OrderBy(x => x.Id).Take(KpiDisputeRules.DocumentLimit + 1).ToArrayAsync(ct);
    }
    public void Add(ApprovalRequest request)
    {
        RequireTransaction();
        if (!request.RequestType.StartsWith(KpiDisputeRules.RequestPrefix, StringComparison.Ordinal) || request.Stage != 1)
            throw new InvalidOperationException("Only KPI dispute requests may be staged here.");
        db.ApprovalRequests.Add(request);
    }
    public void Add(ApprovalDecision decision) { RequireTransaction(); db.ApprovalDecisions.Add(decision); }
    public void Add(AuditEvent audit)
    {
        RequireTransaction();
        if (audit.TargetType != KpiDisputeRules.TargetType || audit.EmployeeScopeId is null || audit.ProtectedChangesReference is null ||
            audit.EventType is not (KpiDisputeRules.SubmittedEvent or KpiDisputeRules.AmendmentEvent))
            throw new InvalidOperationException("Only protected KPI dispute evidence can be appended here.");
        db.AuditEvents.Add(audit);
    }
    public void ExpectVersion(ApprovalRequest request, byte[] version)
    {
        RequireTransaction();
        db.Entry(request).Property(x => x.RowVersion).OriginalValue = version;
    }
    public Task SaveChangesAsync(CancellationToken ct) { RequireTransaction(); return db.SaveChangesAsync(ct); }
}
