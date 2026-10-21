using Cosmix.Domain.Attendance.PayrollInputs;
using Cosmix.Domain.Authentication;
using Cosmix.Domain.HR.Employees;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cosmix.Infrastructure.Database.Configurations.Attendance;

public sealed class AttendancePayrollInputRevisionConfiguration : IEntityTypeConfiguration<AttendancePayrollInputRevision>
{
    public void Configure(EntityTypeBuilder<AttendancePayrollInputRevision> b)
    {
        AttendanceMapping.Record(b, "AttendancePayrollInputRevisions",
            ("Period", "DAY([PeriodStart]) = 1 AND YEAR([PeriodStart]) BETWEEN 1900 AND 9998"),
            ("Revision", "([Revision] = 1 AND [SupersedesRevisionId] IS NULL) OR ([Revision] > 1 AND [SupersedesRevisionId] IS NOT NULL)"),
            ("Fingerprint", "LEN([InputFingerprint]) = 64 AND [InputFingerprint] NOT LIKE '%[^0-9A-F]%'") );
        AttendanceMapping.Strings(b, ("CalculationVersion", 100), ("ProtectedEvidenceReference", 500));
        b.Property(x => x.InputFingerprint).HasMaxLength(64).IsFixedLength().IsUnicode(false).UseCollation("Latin1_General_100_BIN2");
        b.HasIndex(x => new { x.EmployeeId, x.PeriodStart, x.Revision }).IsUnique();
        b.HasOne<Employee>().WithMany().HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<UserAccount>().WithMany().HasForeignKey(x => x.ActorUserId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<AttendancePayrollInputRevision>().WithMany()
            .HasForeignKey(x => new { x.EmployeeId, x.PeriodStart, x.SupersedesRevisionId })
            .HasPrincipalKey(x => new { x.EmployeeId, x.PeriodStart, x.Id }).OnDelete(DeleteBehavior.Restrict);
    }
}
