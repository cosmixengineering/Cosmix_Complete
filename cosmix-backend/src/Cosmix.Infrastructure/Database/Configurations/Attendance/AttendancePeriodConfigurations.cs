using Cosmix.Domain.Attendance.MonthLocks;
using Cosmix.Domain.Common.Approvals;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cosmix.Infrastructure.Database.Configurations.Attendance;

public sealed class AttendancePeriodLockConfiguration : IEntityTypeConfiguration<AttendancePeriodLock>
{
    public void Configure(EntityTypeBuilder<AttendancePeriodLock> b)
    {
        AttendanceMapping.Record(b, "AttendancePeriodLocks", ("Dates", "[PeriodEndExclusive] > [PeriodStart]"), ("Revision", "[Revision] >= 0"));
        b.HasIndex(x => new { x.PeriodStart, x.PeriodEndExclusive }).IsUnique();
    }
}
public sealed class AttendancePeriodLockEventConfiguration : IEntityTypeConfiguration<AttendancePeriodLockEvent>
{
    public void Configure(EntityTypeBuilder<AttendancePeriodLockEvent> b)
    {
        AttendanceMapping.Record(b, "AttendancePeriodLockEvents", ("Revision", "[Revision] > 0"),
            ("Provenance", "([ApprovalRequestId] IS NOT NULL AND [ProtectedEvidenceReference] IS NULL) OR ([ApprovalRequestId] IS NULL AND [ProtectedEvidenceReference] IS NOT NULL AND [IsLocked] = 1)"));
        AttendanceMapping.Strings(b, ("Actor", 200), ("Reason", 2000));
        b.Property(x => x.ProtectedEvidenceReference).HasMaxLength(500);
        b.HasIndex(x => new { x.AttendancePeriodLockId, x.Revision }).IsUnique();
        b.HasOne(x => x.Period).WithMany().HasForeignKey(x => x.AttendancePeriodLockId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<ApprovalRequest>().WithMany().HasForeignKey(x => x.ApprovalRequestId).OnDelete(DeleteBehavior.Restrict);
    }
}
public sealed class AttendancePeriodSummaryConfiguration : IEntityTypeConfiguration<AttendancePeriodSummary>
{
    public void Configure(EntityTypeBuilder<AttendancePeriodSummary> b)
    {
        AttendanceMapping.Record(b, "AttendancePeriodSummaries");
        b.HasIndex(x => new { x.AttendancePeriodLockEventId, x.EmployeeId, x.AttendanceDate }).IsUnique();
        b.HasOne<AttendancePeriodLockEvent>().WithMany().HasForeignKey(x => x.AttendancePeriodLockEventId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Summary).WithMany().HasForeignKey(x => new { x.EmployeeId, x.AttendanceDate, x.DailyAttendanceSummaryId }).HasPrincipalKey(x => new { x.EmployeeId, x.AttendanceDate, x.Id }).OnDelete(DeleteBehavior.Restrict);
    }
}
