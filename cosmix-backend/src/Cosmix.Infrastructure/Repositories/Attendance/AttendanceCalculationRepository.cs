using Cosmix.Application.Attendance.Calculation;
using Cosmix.Application.Attendance.Common;
using Cosmix.Application.Attendance.Corrections;
using Cosmix.Infrastructure.Database;
using Cosmix.Domain.Attendance.Punches;
using Microsoft.EntityFrameworkCore;

namespace Cosmix.Infrastructure.Repositories.Attendance;

/// <summary>Bounded read-only provenance queries. The caller owns authorization and the consistent transaction.</summary>
public sealed class AttendanceCalculationRepository(CosmixDbContext db, AlternativeCorrectionApprovalJournal approvals)
    : IAttendanceCalculationRepository, IAttendanceAlternativeCorrectionApprovalReader
{
    public Task<AttendanceAlternativeCorrectionApprovalProof?> ReadAsync(Guid employeeId, Guid correctionId, Guid decisionId, CancellationToken ct) =>
        approvals.ReadAsync(employeeId, correctionId, decisionId, ct);

    public Task<bool> CorrectionDecisionMatchesAsync(Guid decisionId, Guid approvalId, string evidenceReference, CancellationToken ct) =>
        db.ApprovalDecisions.AsNoTracking().AnyAsync(x => x.Id == decisionId && x.ApprovalRequestId == approvalId &&
            x.ProtectedChangeReference == evidenceReference, ct);

    public async Task<IReadOnlyList<AttendanceCalculationPunch>> PunchesAsync(Guid employeeId, IReadOnlyList<Guid> ids, CancellationToken ct)
    {
        if (employeeId == Guid.Empty || ids.Count > 1024) throw AttendanceOperation.Conflict("attendance_calculation_source_limit");
        return await PunchQuery(employeeId, ids).ToArrayAsync(ct);
    }
    internal IQueryable<AttendanceCalculationPunch> PunchQuery(Guid employeeId, IReadOnlyList<Guid> ids) =>
        from x in db.RawPunchEvents.AsNoTracking()
        join assessment in db.PunchAssessments.AsNoTracking().Where(a =>
            !db.PunchAssessments.Any(later => later.RawPunchEventId == a.RawPunchEventId && later.Revision > a.Revision))
            on new { EmployeeId = x.EmployeeId, PunchId = x.Id } equals new { EmployeeId = assessment.EmployeeId, PunchId = assessment.RawPunchEventId } into matching
        from assessment in matching.DefaultIfEmpty()
        where x.EmployeeId == employeeId && ids.Contains(x.Id)
        orderby x.Id
        select new AttendanceCalculationPunch(x.Id, x.EmployeeId, x.Action,
                x.BusinessDate, x.ReceivedAtUtc, x.SiteId, x.EmployeeAssignmentId, x.ShiftVersionId,
                x.SiteRosterAssignmentId, x.TemporaryShiftChangeId, x.HolidayCalendarId,
                assessment == null ? null : assessment.Id, assessment == null ? null : assessment.Revision,
                assessment == null ? null : assessment.Outcome,
                db.PunchAssessments.Count(a => a.EmployeeId == employeeId && a.RawPunchEventId == x.Id &&
                    !db.PunchAssessments.Any(later => later.RawPunchEventId == a.RawPunchEventId && later.Revision > a.Revision)),
                x.BranchId, x.OfficeRosterAssignmentId, x.OfficeGeofenceVersionId);

    public async Task<IReadOnlyList<AttendanceCalculationOvertimeReference>> OvertimeAsync(Guid employeeId, DateOnly date, CancellationToken ct)
    {
        // Do not filter on valid approval metadata: that would silently hide corrupt requests.
        var rows = await OvertimeQuery(employeeId, date).ToArrayAsync(ct);
        if (rows.Length > 512) throw AttendanceOperation.Conflict("attendance_calculation_source_limit");
        return rows;
    }
    internal IQueryable<AttendanceCalculationOvertimeReference> OvertimeQuery(Guid employeeId, DateOnly date) =>
        (from request in db.OvertimeRequests.AsNoTracking()
            join session in db.WorkSessions.AsNoTracking() on request.WorkSessionId equals session.Id
            where request.EmployeeId == employeeId && session.AttendanceDate == date
            orderby request.Id
            select new AttendanceCalculationOvertimeReference(request.Id,
                db.ApprovalDecisions.Count(d => d.ApprovalRequestId == request.ApprovalRequestId)))
            .Take(513);
}
