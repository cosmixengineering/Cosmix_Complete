using Cosmix.Application.Attendance.Audit;
using Cosmix.Application.Attendance.Common;
using Cosmix.Application.Attendance.Rosters;
using Cosmix.Domain.Attendance.Punches;
using Cosmix.Domain.Attendance.WorkSessions;
using Cosmix.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;

namespace Cosmix.Infrastructure.Repositories.Attendance;

public sealed class AttendanceAuditRepository(CosmixDbContext db) : IAttendanceAuditRepository
{
    internal IQueryable<AttendancePunchAuditRecord> PunchQuery(Guid employeeId, AttendanceAuditRequest request,
        AttendanceCursorPosition? after, int limit)
    {
        var zone = TimeZoneInfo.FindSystemTimeZoneById(RosterSchedule.TimeZoneId);
        var from = request.From.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified);
        var to = request.To.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified);
        var fromUtc = new DateTimeOffset(from, zone.GetUtcOffset(from)).ToUniversalTime();
        var toUtc = new DateTimeOffset(to, zone.GetUtcOffset(to)).ToUniversalTime();
        var query = db.RawPunchEvents.AsNoTracking().Where(x => x.EmployeeId == employeeId &&
            (x.BusinessDate >= request.From && x.BusinessDate < request.To ||
                x.BusinessDate == null && x.ReceivedAtUtc >= fromUtc && x.ReceivedAtUtc < toUtc));
        if (after is not null) query = query.Where(x => x.ReceivedAtUtc > after.CreatedAtUtc ||
            x.ReceivedAtUtc == after.CreatedAtUtc && x.Id.CompareTo(after.Id) > 0);
        return query.OrderBy(x => x.ReceivedAtUtc).ThenBy(x => x.Id).Take(limit)
            .Select(x => new AttendancePunchAuditRecord(x, db.PunchAssessments.AsNoTracking()
                .Where(a => a.RawPunchEventId == x.Id && a.EmployeeId == employeeId)
                .OrderByDescending(a => a.Revision).FirstOrDefault()));
    }

    internal IQueryable<AttendanceSessionAuditRecord> SessionQuery(Guid employeeId, AttendanceAuditRequest request,
        AttendanceCursorPosition? after, int limit)
    {
        var query = db.WorkSessions.AsNoTracking().Where(x => x.EmployeeId == employeeId &&
            x.AttendanceDate >= request.From && x.AttendanceDate < request.To);
        if (after is not null) query = query.Where(x => x.CalculatedAtUtc > after.CreatedAtUtc ||
            x.CalculatedAtUtc == after.CreatedAtUtc && x.Id.CompareTo(after.Id) > 0);
        // Deliberately retain all immutable revisions. Latest is a label, never a replacement filter.
        return query.OrderBy(x => x.CalculatedAtUtc).ThenBy(x => x.Id).Take(limit)
            .Select(x => new AttendanceSessionAuditRecord(x, x.CheckInPunch, x.CheckOutPunch,
                !db.WorkSessions.Any(y => y.EmployeeId == employeeId && y.SessionKey == x.SessionKey && y.Revision > x.Revision)));
    }

    public async Task<IReadOnlyList<AttendancePunchAuditRecord>> PunchesAsync(Guid employeeId, AttendanceAuditRequest request,
        AttendanceCursorPosition? after, int limit, CancellationToken ct) => await PunchQuery(employeeId, request, after, limit).ToArrayAsync(ct);
    public async Task<IReadOnlyList<AttendanceSessionAuditRecord>> SessionsAsync(Guid employeeId, AttendanceAuditRequest request,
        AttendanceCursorPosition? after, int limit, CancellationToken ct) => await SessionQuery(employeeId, request, after, limit).ToArrayAsync(ct);
}
