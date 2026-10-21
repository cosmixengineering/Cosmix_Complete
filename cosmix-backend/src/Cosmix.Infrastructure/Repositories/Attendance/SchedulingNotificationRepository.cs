using Cosmix.Application.Attendance.Shifts;
using Cosmix.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;

namespace Cosmix.Infrastructure.Repositories.Attendance;

public sealed class SchedulingNotificationRepository(CosmixDbContext db) : ISchedulingNotificationRepository
{
    public async Task<IReadOnlyList<SchedulingNotificationRecipient>> AccountsAsync(IReadOnlyList<Guid> employeeIds, CancellationToken ct)
    {
        if (employeeIds.Count is < 1 or > 2000) throw SchedulingOperation.Conflict("scheduling_notification_manifest_invalid");
        return await db.UserAccounts.AsNoTracking().Where(x => employeeIds.Contains(x.EmployeeId))
            .OrderBy(x => x.EmployeeId).Select(x => new SchedulingNotificationRecipient(x.EmployeeId, x.Id)).Take(2001).ToArrayAsync(ct);
    }
}
