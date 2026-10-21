using Cosmix.Domain.Attendance.MonthLocks;
using Microsoft.EntityFrameworkCore;

namespace Cosmix.Infrastructure.Database;

/// <summary>Keep the mutable period pointer and immutable lock event in the same write.
/// A retained locked period cannot be reopened or moved to a different date range.</summary>
internal static class AttendancePeriodWriteGuard
{
    internal static void Validate(CosmixDbContext db)
    {
        var events = db.ChangeTracker.Entries<AttendancePeriodLockEvent>()
            .Where(x => x.State == EntityState.Added).ToArray();
        var periods = db.ChangeTracker.Entries<AttendancePeriodLock>().ToDictionary(x => x.Entity.Id);
        foreach (var period in periods.Values.Where(x => x.State is EntityState.Added or EntityState.Modified or EntityState.Deleted))
        {
            if (period.State == EntityState.Deleted)
                throw new InvalidOperationException("Attendance periods and their retained evidence cannot be deleted.");
            if (period.State == EntityState.Modified &&
                (period.Property(x => x.PeriodStart).IsModified || period.Property(x => x.PeriodEndExclusive).IsModified ||
                 period.Property(x => x.IsLocked).OriginalValue || !period.Entity.IsLocked ||
                 period.Entity.Revision != checked(period.Property(x => x.Revision).OriginalValue + 1)))
                throw new InvalidOperationException("Attendance period dates are fixed and a locked period cannot reopen or change revision.");
            if (!period.Entity.IsLocked)
            {
                if (period.Entity.Revision != 0) throw new InvalidOperationException("A new open period cannot fabricate retained revisions.");
                continue;
            }
            if (period.State == EntityState.Added && period.Entity.Revision != 1 ||
                events.Count(x => x.Entity.AttendancePeriodLockId == period.Entity.Id && x.Entity.IsLocked && x.Entity.Revision == period.Entity.Revision) != 1)
                throw new InvalidOperationException("Save a newly locked attendance period with exactly one matching lock event.");
        }
        foreach (var item in events)
        {
            if (!item.Entity.IsLocked || !periods.TryGetValue(item.Entity.AttendancePeriodLockId, out var period) ||
                period.State is not (EntityState.Added or EntityState.Modified) || !period.Entity.IsLocked ||
                item.Entity.Revision != period.Entity.Revision)
                throw new InvalidOperationException("A lock event requires its tracked parent transition; reopening is forbidden.");
        }
    }
}
