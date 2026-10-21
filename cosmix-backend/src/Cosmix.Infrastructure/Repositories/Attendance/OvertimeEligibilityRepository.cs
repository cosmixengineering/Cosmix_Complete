using Cosmix.Application.Attendance.Common;
using Cosmix.Application.Attendance.Overtime;
using Cosmix.Domain.Attendance.Overtime;
using Cosmix.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;

namespace Cosmix.Infrastructure.Repositories.Attendance;

public sealed class OvertimeEligibilityRepository(CosmixDbContext db) : IOvertimeEligibilityRepository
{
    internal IQueryable<OvertimeEligibilityVersion> TimelineQuery(OvertimeEligibilityTarget target, Guid? employeeId) =>
        db.Set<OvertimeEligibilityVersion>().AsNoTracking().Where(x => x.Target == target && x.EmployeeId == employeeId).OrderBy(x => x.Revision).Take(513);
    public async Task<IReadOnlyList<OvertimeEligibilityVersion>> TimelineAsync(OvertimeEligibilityTarget target, Guid? employeeId, CancellationToken ct)
    {
        var rows = await TimelineQuery(target, employeeId).ToArrayAsync(ct);
        if (rows.Length > 512) throw AttendanceOperation.Conflict("overtime_eligibility_history_limit");
        return rows;
    }
    public void Add(OvertimeEligibilityVersion version) => db.Set<OvertimeEligibilityVersion>().Add(version);
    public async Task SaveChangesAsync(CancellationToken ct) => await db.SaveChangesAsync(ct);
}
