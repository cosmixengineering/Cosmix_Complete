using Cosmix.Application.Attendance.Corrections;
using Cosmix.Domain.Common.Audit;
using Cosmix.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;

namespace Cosmix.Infrastructure.Repositories.Attendance;

public sealed class AlternativeCorrectionApprovalRepository(CosmixDbContext db) : IAlternativeCorrectionApprovalRepository
{
    public Guid? TransactionId => db.Database.CurrentTransaction?.TransactionId;
    public async Task<AlternativeCorrectionApprovalSource?> SourceAsync(Guid employeeId, Guid correctionId, Guid decisionId, CancellationToken ct)
    {
        var rows = await SourceQuery(employeeId, correctionId, decisionId).Take(2).ToArrayAsync(ct);
        return rows.Length == 1 ? rows[0] : null;
    }
    internal IQueryable<AlternativeCorrectionApprovalSource> SourceQuery(Guid employeeId, Guid correctionId, Guid decisionId) =>
        from correction in db.AttendanceCorrections.AsNoTracking()
        join approval in db.ApprovalRequests.AsNoTracking() on correction.ApprovalRequestId equals approval.Id
        join decision in db.ApprovalDecisions.AsNoTracking() on approval.Id equals decision.ApprovalRequestId
        join session in db.WorkSessions.AsNoTracking() on correction.Id equals session.CorrectionId
        where correction.EmployeeId == employeeId && correction.Id == correctionId && decision.Id == decisionId &&
            session.EmployeeId == employeeId && session.CorrectionId == correctionId && session.AttendanceDate == correction.AttendanceDate &&
            db.ApprovalDecisions.Count(x => x.ApprovalRequestId == approval.Id) == 1
        select new AlternativeCorrectionApprovalSource(correction, approval, decision, session);
    internal IQueryable<AuditEvent> AuthorityQuery(Guid employeeId, Guid decisionId) => db.AuditEvents.AsNoTracking()
        .Where(x => x.EventType == AlternativeCorrectionApprovalJournal.AuthorityOperation && x.TargetType == AlternativeCorrectionApprovalJournal.AuthorityTarget &&
            x.EmployeeScopeId == employeeId && x.TargetId == decisionId.ToString()).OrderBy(x => x.Id).Take(2);
    internal IQueryable<AuditEvent> DecisionQuery(Guid employeeId, Guid correctionId) => db.AuditEvents.AsNoTracking()
        .Where(x => x.EventType == "attendance.correction.decide" && x.TargetType == "Attendance" &&
            x.EmployeeScopeId == employeeId && x.TargetId == correctionId.ToString()).OrderBy(x => x.Id).Take(2);
    public async Task<IReadOnlyList<AuditEvent>> AuthorityAuditsAsync(Guid employeeId, Guid decisionId, CancellationToken ct) =>
        await AuthorityQuery(employeeId, decisionId).ToArrayAsync(ct);
    public async Task<IReadOnlyList<AuditEvent>> DecisionAuditsAsync(Guid employeeId, Guid correctionId, CancellationToken ct) =>
        await DecisionQuery(employeeId, correctionId).ToArrayAsync(ct);
}
