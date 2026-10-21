using Cosmix.Application.Attendance.PayrollInputs;
using Cosmix.Application.Common.Models;
using Cosmix.Domain.Attendance.PayrollInputs;
using Cosmix.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;

namespace Cosmix.Infrastructure.Repositories.Attendance;

public sealed class AttendancePayrollInputLedger(CosmixDbContext db) : IAttendancePayrollInputLedger
{
    private IQueryable<AttendancePayrollInputRevision> Revisions(Guid employeeId, DateOnly monthStart) =>
        db.Set<AttendancePayrollInputRevision>().AsNoTracking()
            .Where(x => x.EmployeeId == employeeId && x.PeriodStart == monthStart);
    public Task<bool> IsLockedAsync(DateOnly monthStart, CancellationToken ct)
    {
        var end = monthStart.AddMonths(1);
        return db.AttendancePeriodLocks.AnyAsync(x => x.IsLocked && x.PeriodStart < end && x.PeriodEndExclusive > monthStart, ct);
    }
    public Task<AttendancePayrollInputRevision?> LatestAsync(Guid employeeId, DateOnly monthStart, CancellationToken ct) =>
        Revisions(employeeId, monthStart).OrderByDescending(x => x.Revision).FirstOrDefaultAsync(ct);
    public Task<AttendancePayrollInputRevision?> FindAsync(Guid employeeId, Guid id, CancellationToken ct) =>
        db.Set<AttendancePayrollInputRevision>().AsNoTracking().SingleOrDefaultAsync(x => x.EmployeeId == employeeId && x.Id == id, ct);
    public async Task<PagedResult<AttendancePayrollInputRevision>> HistoryAsync(Guid employeeId, DateOnly monthStart,
        int page, int pageSize, CancellationToken ct)
    {
        var query = Revisions(employeeId, monthStart);
        return new(await query.OrderByDescending(x => x.Revision).Skip((page - 1) * pageSize).Take(pageSize).ToArrayAsync(ct),
            await query.CountAsync(ct), page, pageSize);
    }
    public void Add(AttendancePayrollInputRevision revision) => db.Set<AttendancePayrollInputRevision>().Add(revision);
}
