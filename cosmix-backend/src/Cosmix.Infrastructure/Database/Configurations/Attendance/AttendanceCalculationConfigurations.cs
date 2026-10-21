using Cosmix.Domain.Attendance.Corrections;
using Cosmix.Domain.Attendance.DailySummaries;
using Cosmix.Domain.Attendance.WorkSessions;
using Cosmix.Domain.Attendance.Shifts;
using Cosmix.Domain.Attendance.Sites;
using Cosmix.Domain.HR.Employees;
using Cosmix.Domain.HR.Policies;
using Cosmix.Domain.HR.Branches;
using Cosmix.Domain.HR.EmployeeAssignments;
using Cosmix.Domain.HR.Holidays;
using Cosmix.Domain.Attendance.Rosters;
using Cosmix.Domain.Attendance.Offices;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cosmix.Infrastructure.Database.Configurations.Attendance;

public sealed class WorkSessionConfiguration : IEntityTypeConfiguration<WorkSession>
{
    public void Configure(EntityTypeBuilder<WorkSession> b)
    {
        AttendanceMapping.Record(b, "WorkSessions", ("Revision", "[Revision] > 0"), ("Window", "[EndsAtUtc] IS NULL OR [EndsAtUtc] > [StartsAtUtc]"),
            ("Evidence", "[CorrectionId] IS NOT NULL OR ([CheckInPunchId] IS NOT NULL AND ([EndsAtUtc] IS NULL OR [CheckOutPunchId] IS NOT NULL))"),
            ("CheckOut", "[CheckOutPunchId] IS NULL OR [EndsAtUtc] IS NOT NULL"), ("ContinuousDuty", "[EndsContinuousDuty] = 0 OR [EndsAtUtc] IS NOT NULL"),
            ("OfficeContext", "([BranchId] IS NULL AND [OfficeRosterAssignmentId] IS NULL AND [OfficeGeofenceVersionId] IS NULL) OR " +
                "([BranchId] IS NOT NULL AND [SiteId] IS NULL AND [OfficeRosterAssignmentId] IS NOT NULL AND [EmployeeAssignmentId] IS NOT NULL AND [OfficeGeofenceVersionId] IS NOT NULL)"));
        b.HasIndex(x => new { x.SessionKey, x.Revision }).IsUnique(); b.HasIndex(x => new { x.EmployeeId, x.AttendanceDate });
        b.HasOne<Employee>().WithMany().HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.CheckInPunch).WithMany().HasForeignKey(x => new { x.EmployeeId, x.CheckInPunchId }).HasPrincipalKey(x => new { x.EmployeeId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.CheckOutPunch).WithMany().HasForeignKey(x => new { x.EmployeeId, x.CheckOutPunchId }).HasPrincipalKey(x => new { x.EmployeeId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<AttendanceCorrection>().WithMany().HasForeignKey(x => new { x.EmployeeId, x.CorrectionId }).HasPrincipalKey(x => new { x.EmployeeId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Site>().WithMany().HasForeignKey(x => x.SiteId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<ShiftVersion>().WithMany().HasForeignKey(x => x.ShiftVersionId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<PolicyVersion>().WithMany().HasForeignKey(x => x.PolicyVersionId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Branch>().WithMany().HasForeignKey(x => x.BranchId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<OfficeRosterAssignment>().WithMany().HasForeignKey(x => new { x.BranchId, x.OfficeRosterAssignmentId })
            .HasPrincipalKey(x => new { x.BranchId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<OfficeGeofenceVersion>().WithMany().HasForeignKey(x => new { x.BranchId, x.OfficeGeofenceVersionId })
            .HasPrincipalKey(x => new { x.BranchId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<EmployeeAssignment>().WithMany().HasForeignKey(x => new { x.EmployeeId, x.EmployeeAssignmentId })
            .HasPrincipalKey(x => new { x.EmployeeId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<HolidayCalendar>().WithMany().HasForeignKey(x => x.HolidayCalendarId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class DailyAttendanceSummaryConfiguration : IEntityTypeConfiguration<DailyAttendanceSummary>
{
    public void Configure(EntityTypeBuilder<DailyAttendanceSummary> b)
    {
        AttendanceMapping.Record(b, "DailyAttendanceSummaries", ("Revision", "[Revision] > 0"), ("Status", "[Status] BETWEEN 1 AND 9"),
            ("Minutes", "[ScheduledMinutes] >= 0 AND [WorkedMinutes] >= 0 AND [PayableMinutes] >= 0 AND [LateMinutes] >= 0 AND [OvertimeMinutes] >= 0 AND [ExceptionCount] >= 0"),
            ("Fingerprint", "LEN([InputFingerprint]) = 64 AND [InputFingerprint] NOT LIKE '%[^0-9A-F]%'"),
            ("PolicyOutputs", "([PolicyVersionId] IS NULL AND [PayableMinutes] IS NULL AND [LateMinutes] IS NULL AND [OvertimeMinutes] IS NULL AND [Status] IN (1,9)) OR ([PolicyVersionId] IS NOT NULL AND [PayableMinutes] IS NOT NULL AND [LateMinutes] IS NOT NULL AND [OvertimeMinutes] IS NOT NULL)"));
        AttendanceMapping.Strings(b, ("CalculationVersion", 100), ("InputEvidenceReference", 500));
        b.Property(x => x.InputFingerprint).HasMaxLength(64).IsFixedLength().IsUnicode(false).UseCollation("Latin1_General_100_BIN2");
        b.HasIndex(x => new { x.EmployeeId, x.AttendanceDate, x.Revision }).IsUnique();
        b.HasOne<Employee>().WithMany().HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<ShiftVersion>().WithMany().HasForeignKey(x => x.ShiftVersionId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<PolicyVersion>().WithMany().HasForeignKey(x => x.PolicyVersionId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<DailyAttendanceSummary>().WithMany().HasForeignKey(x => new { x.EmployeeId, x.AttendanceDate, x.SupersedesSummaryId })
            .HasPrincipalKey(x => new { x.EmployeeId, x.AttendanceDate, x.Id }).OnDelete(DeleteBehavior.Restrict);
        b.Navigation(x => x.CalculationLines).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

public sealed class AttendanceCalculationLineConfiguration : IEntityTypeConfiguration<AttendanceCalculationLine>
{
    public void Configure(EntityTypeBuilder<AttendanceCalculationLine> b)
    {
        AttendanceMapping.Record(b, "AttendanceCalculationLines", ("Sequence", "[Sequence] > 0"),
            ("Currency", "([MonetaryAmount] IS NULL AND [Currency] IS NULL) OR ([MonetaryAmount] IS NOT NULL AND [Currency] IS NOT NULL AND LEN([Currency]) = 3)"));
        AttendanceMapping.Strings(b, ("RuleCode", 100), ("Explanation", 2000), ("UnitCode", 30), ("Currency", 3));
        b.Property(x => x.Currency).IsUnicode(false); b.Property(x => x.InputQuantity).HasPrecision(18, 4);
        b.Property(x => x.ResultQuantity).HasPrecision(18, 4); b.Property(x => x.MonetaryAmount).HasPrecision(18, 2);
        b.HasIndex(x => new { x.DailyAttendanceSummaryId, x.Sequence }).IsUnique();
        b.HasOne(x => x.Summary).WithMany(x => x.CalculationLines).HasForeignKey(x => x.DailyAttendanceSummaryId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class AttendanceSummarySessionConfiguration : IEntityTypeConfiguration<AttendanceSummarySession>
{
    public void Configure(EntityTypeBuilder<AttendanceSummarySession> b)
    {
        AttendanceMapping.Record(b, "AttendanceSummarySessions", ("Minutes", "[AllocatedMinutes] >= 0"));
        b.HasIndex(x => new { x.DailyAttendanceSummaryId, x.WorkSessionId }).IsUnique();
        b.HasOne(x => x.Summary).WithMany().HasForeignKey(x => new { x.EmployeeId, x.DailyAttendanceSummaryId }).HasPrincipalKey(x => new { x.EmployeeId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.WorkSession).WithMany().HasForeignKey(x => new { x.EmployeeId, x.WorkSessionId }).HasPrincipalKey(x => new { x.EmployeeId, x.Id }).OnDelete(DeleteBehavior.Restrict);
    }
}
