using Cosmix.Domain.Attendance.Overtime;
using Cosmix.Domain.HR.Employees;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cosmix.Infrastructure.Database.Configurations.Attendance;

public sealed class OvertimeEligibilityVersionConfiguration : IEntityTypeConfiguration<OvertimeEligibilityVersion>
{
    public void Configure(EntityTypeBuilder<OvertimeEligibilityVersion> b)
    {
        AttendanceMapping.Record(b, "OvertimeEligibilityVersions",
            ("Target", "([Target] = 1 AND [EmployeeId] IS NULL) OR ([Target] = 2 AND [EmployeeId] IS NOT NULL)"),
            ("Revision", "([Revision] = 1 AND [SupersedesVersionId] IS NULL) OR ([Revision] > 1 AND [SupersedesVersionId] IS NOT NULL)"));
        AttendanceMapping.Strings(b, ("RecordedBy", 200), ("Reason", 1000), ("ProtectedEvidenceReference", 500));
        b.HasIndex(x => new { x.Target, x.EmployeeId, x.Revision }).IsUnique().HasFilter(null);
        b.HasIndex(x => new { x.Target, x.EmployeeId, x.EffectiveFrom, x.Revision });
        b.HasOne<Employee>().WithMany().HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<OvertimeEligibilityVersion>().WithMany().HasForeignKey(x => x.SupersedesVersionId).OnDelete(DeleteBehavior.Restrict);
    }
}
