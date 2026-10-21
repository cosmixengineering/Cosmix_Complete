using Cosmix.Domain.Attendance.Corrections;
using Cosmix.Domain.Attendance.DailySummaries;
using Cosmix.Domain.Attendance.WorkSessions;
using Cosmix.Domain.Attendance.Punches;
using Cosmix.Domain.Attendance.MonthLocks;
using Cosmix.Domain.Common.Approvals;
using Cosmix.Domain.HR.Employees;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cosmix.Infrastructure.Database.Configurations.Attendance;

public sealed class AttendanceExceptionConfiguration : IEntityTypeConfiguration<AttendanceException>
{
    public void Configure(EntityTypeBuilder<AttendanceException> b)
    {
        AttendanceMapping.Record(b, "AttendanceExceptions", ("Kind", "[Kind] BETWEEN 1 AND 10"));
        AttendanceMapping.Strings(b, ("Reason", 1000)); b.HasIndex(x => new { x.EmployeeId, x.AttendanceDate });
        b.HasOne<Employee>().WithMany().HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<ApprovalRequest>().WithMany().HasForeignKey(x => x.ApprovalRequestId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<RawPunchEvent>().WithMany().HasForeignKey(x => new { x.EmployeeId, x.RawPunchEventId }).HasPrincipalKey(x => new { x.EmployeeId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<WorkSession>().WithMany().HasForeignKey(x => new { x.EmployeeId, x.WorkSessionId }).HasPrincipalKey(x => new { x.EmployeeId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<DailyAttendanceSummary>().WithMany().HasForeignKey(x => new { x.EmployeeId, x.DailyAttendanceSummaryId }).HasPrincipalKey(x => new { x.EmployeeId, x.Id }).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class AttendanceCorrectionConfiguration : IEntityTypeConfiguration<AttendanceCorrection>
{
    public void Configure(EntityTypeBuilder<AttendanceCorrection> b)
    {
        AttendanceMapping.Record(b, "AttendanceCorrections", ("Kind", "[Kind] BETWEEN 1 AND 4"),
            ("Status", "([OriginalStatus] IS NULL OR [OriginalStatus] BETWEEN 1 AND 9) AND ([RequestedStatus] IS NULL OR [RequestedStatus] BETWEEN 1 AND 9)"),
            ("TypedChange", "([Kind] = 3 AND [RequestedStatus] IS NOT NULL AND [RequestedTimeUtc] IS NULL AND [RequestedEndUtc] IS NULL) OR ([Kind] IN (1,2) AND [RequestedTimeUtc] IS NOT NULL AND [RequestedEndUtc] IS NULL AND [RequestedStatus] IS NULL) OR ([Kind] = 4 AND [RequestedTimeUtc] IS NOT NULL AND [RequestedEndUtc] IS NOT NULL AND [RequestedEndUtc] > [RequestedTimeUtc] AND [RequestedStatus] IS NULL)"));
        AttendanceMapping.Strings(b, ("RequestedBy", 200), ("Reason", 2000), ("PrivateEvidenceReference", 500));
        b.HasIndex(x => new { x.EmployeeId, x.AttendanceDate }); b.HasIndex(x => x.ApprovalRequestId).IsUnique();
        b.HasOne<Employee>().WithMany().HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<ApprovalRequest>().WithMany().HasForeignKey(x => x.ApprovalRequestId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<RawPunchEvent>().WithMany().HasForeignKey(x => new { x.EmployeeId, x.RawPunchEventId }).HasPrincipalKey(x => new { x.EmployeeId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<DailyAttendanceSummary>().WithMany().HasForeignKey(x => new { x.EmployeeId, x.AttendanceDate, x.OriginalSummaryId }).HasPrincipalKey(x => new { x.EmployeeId, x.AttendanceDate, x.Id }).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<AttendancePeriodLock>().WithMany().HasForeignKey(x => x.AdjustmentPeriodLockId).OnDelete(DeleteBehavior.Restrict);
    }
}
