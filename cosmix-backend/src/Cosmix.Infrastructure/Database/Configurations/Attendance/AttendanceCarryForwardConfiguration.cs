using Cosmix.Domain.Attendance.CarryForward;
using Cosmix.Domain.Attendance.Corrections;
using Cosmix.Domain.Attendance.DailySummaries;
using Cosmix.Domain.Attendance.MonthLocks;
using Cosmix.Domain.Common.Approvals;
using Cosmix.Domain.HR.Employees;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cosmix.Infrastructure.Database.Configurations.Attendance;

public sealed class AttendanceCarryForwardConfiguration : IEntityTypeConfiguration<AttendanceCarryForwardAdjustment>
{
    public void Configure(EntityTypeBuilder<AttendanceCarryForwardAdjustment> b)
    {
        AttendanceMapping.Record(b, "AttendanceCarryForwardAdjustments",
            ("Revision", "([Revision] = 1 AND [SupersedesAdjustmentId] IS NULL) OR ([Revision] > 1 AND [SupersedesAdjustmentId] IS NOT NULL)"),
            ("Month", "DAY([TargetMonthStart]) = 1 AND [TargetMonthEndExclusive] = DATEADD(month, 1, [TargetMonthStart]) AND [TargetMonthStart] > DATEFROMPARTS(YEAR([SourceBusinessDate]), MONTH([SourceBusinessDate]), 1)"),
            ("Ticks", "[BeforeWorkedTicks] >= 0 AND [AfterWorkedTicks] >= 0 AND [DeltaWorkedTicks] = [AfterWorkedTicks] - [BeforeWorkedTicks]"),
            ("Window", "[LastEndsAtUtc] > [FirstStartsAtUtc]"),
            ("Fingerprints", "LEN([InputFingerprint]) = 64 AND [InputFingerprint] NOT LIKE '%[^0-9A-F]%' AND LEN([InterpretationFingerprint]) = 64 AND [InterpretationFingerprint] NOT LIKE '%[^0-9A-F]%'"));
        AttendanceMapping.Strings(b, ("ProtectedEvidenceReference", 500));
        foreach (var property in new[] { "InputFingerprint", "InterpretationFingerprint" })
            b.Property<string>(property).HasMaxLength(64).IsFixedLength().IsUnicode(false).UseCollation("Latin1_General_100_BIN2");
        b.HasIndex(x => x.CorrectionId).IsUnique(); b.HasIndex(x => x.ApprovalDecisionId).IsUnique();
        b.HasIndex(x => new { x.EmployeeId, x.SourceBusinessDate, x.Revision }).IsUnique();
        b.HasIndex(x => new { x.EmployeeId, x.TargetMonthStart });
        b.HasIndex(x => new { x.EmployeeId, x.FirstStartsAtUtc, x.LastEndsAtUtc });
        b.HasOne<Employee>().WithMany().HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<AttendancePeriodLockEvent>().WithMany().HasForeignKey(x => x.SourcePeriodLockEventId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<DailyAttendanceSummary>().WithMany().HasForeignKey(x => new { x.EmployeeId, x.SourceBusinessDate, x.SourceSummaryId })
            .HasPrincipalKey(x => new { x.EmployeeId, x.AttendanceDate, x.Id }).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<AttendanceCorrection>().WithMany().HasForeignKey(x => new { x.EmployeeId, x.CorrectionId })
            .HasPrincipalKey(x => new { x.EmployeeId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<ApprovalDecision>().WithMany().HasForeignKey(x => x.ApprovalDecisionId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<AttendanceCarryForwardAdjustment>().WithMany().HasForeignKey(x => new { x.EmployeeId, x.SourceBusinessDate, x.SupersedesAdjustmentId })
            .HasPrincipalKey(x => new { x.EmployeeId, x.SourceBusinessDate, x.Id }).OnDelete(DeleteBehavior.Restrict);
    }
}
