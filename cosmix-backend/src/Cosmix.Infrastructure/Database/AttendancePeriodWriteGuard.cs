using Cosmix.Domain.Attendance.DailySummaries;
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
        var memberships = db.ChangeTracker.Entries<AttendancePeriodSummary>()
            .Where(x => x.State == EntityState.Added).Select(x => x.Entity)
            .ToLookup(x => x.AttendancePeriodLockEventId);
        var summaries = db.ChangeTracker.Entries<DailyAttendanceSummary>().ToDictionary(x => x.Entity.Id);
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
            // Legacy approval-backed events remain readable, but cannot authorize a new lock.
            // Protected manifest contents and full readiness are checked by MonthLockService.
            if (item.Entity.Id == Guid.Empty || item.Entity.ApprovalRequestId.HasValue ||
                string.IsNullOrWhiteSpace(item.Entity.ProtectedEvidenceReference))
                throw new InvalidOperationException("New attendance locks require direct protected manifest evidence, not a legacy approval.");
            var links = memberships[item.Entity.Id].ToArray();
            if (links.Length == 0 ||
                links.Select(x => x.Id).Distinct().Count() != links.Length ||
                links.Select(x => x.DailyAttendanceSummaryId).Distinct().Count() != links.Length ||
                links.Select(x => (x.EmployeeId, x.AttendanceDate)).Distinct().Count() != links.Length ||
                links.Any(x => x.Id == Guid.Empty || x.EmployeeId == Guid.Empty || x.DailyAttendanceSummaryId == Guid.Empty ||
                    x.AttendanceDate < period.Entity.PeriodStart || x.AttendanceDate >= period.Entity.PeriodEndExclusive))
                throw new InvalidOperationException("New attendance locks require nonempty, unique summary membership within their period.");
            foreach (var link in links)
            {
                // Finalization reads saved summaries without tracking. The existing composite
                // FK enforces persisted employee/date/summary identity in that case; do not
                // query SQL from the synchronous SaveChanges guard or require tracked reads.
                if (summaries.TryGetValue(link.DailyAttendanceSummaryId, out var summary) &&
                    (summary.State is EntityState.Modified or EntityState.Deleted ||
                     summary.Entity.EmployeeId != link.EmployeeId || summary.Entity.AttendanceDate != link.AttendanceDate))
                    throw new InvalidOperationException("Locked membership must reference the exact immutable employee/date summary revision.");
            }
        }
    }
}
