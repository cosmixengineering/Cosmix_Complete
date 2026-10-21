using Cosmix.Domain.Attendance.Rosters;
using Cosmix.Domain.Attendance.Shifts;
using Cosmix.Domain.Attendance.Sites;
using Cosmix.Domain.Authentication.Scopes;
using Cosmix.Domain.Common.Approvals;
using Cosmix.Domain.HR.Employees;
using Cosmix.Domain.HR.Departments;
using Cosmix.Domain.HR.StaffCategories;
using Cosmix.Domain.HR.Holidays;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cosmix.Infrastructure.Database.Configurations.Attendance;

public sealed class ShiftPopulationAssignmentConfiguration : IEntityTypeConfiguration<ShiftPopulationAssignment>
{
    public void Configure(EntityTypeBuilder<ShiftPopulationAssignment> b)
    {
        AttendanceMapping.Record(b, "ShiftPopulationAssignments", ("Dates", "[EffectiveTo] IS NULL OR [EffectiveTo] > [EffectiveFrom]"),
            ("Population", "([PopulationKind] = 1 AND [TeamId] IS NOT NULL AND [SiteId] IS NULL AND [StaffCategoryId] IS NULL AND [DepartmentId] IS NULL) OR ([PopulationKind] = 2 AND [TeamId] IS NULL AND [SiteId] IS NOT NULL AND [StaffCategoryId] IS NULL AND [DepartmentId] IS NULL) OR ([PopulationKind] = 3 AND [TeamId] IS NULL AND [SiteId] IS NULL AND [StaffCategoryId] IS NOT NULL AND [DepartmentId] IS NULL) OR ([PopulationKind] = 4 AND [TeamId] IS NULL AND [SiteId] IS NULL AND [StaffCategoryId] IS NULL AND [DepartmentId] IS NOT NULL)"));
        b.HasIndex(x => new { x.TeamId, x.EffectiveFrom }); b.HasIndex(x => new { x.SiteId, x.EffectiveFrom });
        b.HasIndex(x => new { x.StaffCategoryId, x.EffectiveFrom }); b.HasIndex(x => new { x.DepartmentId, x.EffectiveFrom });
        b.HasOne<Shift>().WithMany().HasForeignKey(x => x.ShiftId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Team>().WithMany().HasForeignKey(x => x.TeamId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Site>().WithMany().HasForeignKey(x => x.SiteId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<StaffCategory>().WithMany().HasForeignKey(x => x.StaffCategoryId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Department>().WithMany().HasForeignKey(x => x.DepartmentId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<ApprovalRequest>().WithMany().HasForeignKey(x => x.ApprovalRequestId).IsRequired(false).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<HolidayCalendar>().WithMany().HasForeignKey(x => x.HolidayCalendarId).IsRequired(false).OnDelete(DeleteBehavior.Restrict);
    }
}
public sealed class TemporaryShiftChangeConfiguration : IEntityTypeConfiguration<TemporaryShiftChange>
{
    public void Configure(EntityTypeBuilder<TemporaryShiftChange> b)
    {
        AttendanceMapping.Record(b, "TemporaryShiftChanges", ("Versions", "[OriginalShiftVersionId] <> [ReplacementShiftVersionId]"));
        AttendanceMapping.Strings(b, ("ApprovedBy", 200), ("WrittenEvidenceReference", 500), ("Reason", 2000));
        b.HasIndex(x => new { x.SiteId, x.AttendanceDate });
        b.HasOne<Site>().WithMany().HasForeignKey(x => x.SiteId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<ShiftVersion>().WithMany().HasForeignKey(x => x.OriginalShiftVersionId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<ShiftVersion>().WithMany().HasForeignKey(x => x.ReplacementShiftVersionId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<ApprovalRequest>().WithMany().HasForeignKey(x => x.ApprovalRequestId).IsRequired(false).OnDelete(DeleteBehavior.Restrict);
        b.Navigation(x => x.Employees).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
public sealed class TemporaryShiftChangeEmployeeConfiguration : IEntityTypeConfiguration<TemporaryShiftChangeEmployee>
{
    public void Configure(EntityTypeBuilder<TemporaryShiftChangeEmployee> b)
    {
        AttendanceMapping.Record(b, "TemporaryShiftChangeEmployees"); b.HasIndex(x => new { x.TemporaryShiftChangeId, x.EmployeeId }).IsUnique();
        b.HasOne(x => x.Change).WithMany(x => x.Employees).HasForeignKey(x => x.TemporaryShiftChangeId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Employee>().WithMany().HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.Restrict);
    }
}
