using Cosmix.Domain.Attendance.PayrollInputs;
using Cosmix.Domain.Authentication;
using Cosmix.Domain.HR.Employees;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cosmix.Infrastructure.Database.Configurations.Attendance;

public sealed class AttendancePayrollAttestationConfiguration : IEntityTypeConfiguration<AttendancePayrollAttestation>
{
    public void Configure(EntityTypeBuilder<AttendancePayrollAttestation> b)
    {
        AttendanceMapping.Record(b, "AttendancePayrollAttestations",
            ("Period", "DAY([PeriodStart]) = 1 AND YEAR([PeriodStart]) BETWEEN 1900 AND 9998"),
            ("SourceRevision", "[SourceRevision] >= 1"),
            ("Stage", "([Status] = 'handoff' AND [Step] = 1 AND [PreviousAttestationId] IS NULL AND [PostedAtUtc] IS NULL) OR ([Status] = 'posted' AND [Step] = 2 AND [PreviousAttestationId] IS NOT NULL AND [PostedAtUtc] IS NOT NULL AND [PostedAtUtc] = [OccurredAtUtc]) OR ([Status] = 'settled' AND [Step] = 3 AND [PreviousAttestationId] IS NOT NULL AND [PostedAtUtc] IS NOT NULL AND [PostedAtUtc] <= [OccurredAtUtc])"),
            ("Time", "[OccurredAtUtc] <= [CreatedAtUtc]"));
        AttendanceMapping.Strings(b, ("Status", 16), ("ProtectedEvidenceReference", 500));
        b.Property(x => x.Status).IsUnicode(false).UseCollation("Latin1_General_100_BIN2");
        foreach (var name in new[] { "SourceFingerprint", "LineFingerprint", "ExternalReceiptKey", "EvidenceFingerprint" })
        {
            b.Property<string>(name).HasMaxLength(64).IsFixedLength().IsUnicode(false).UseCollation("Latin1_General_100_BIN2");
            b.ToTable("AttendancePayrollAttestations", "attendance", table =>
                table.HasCheckConstraint($"CK_AttendancePayrollAttestations_{name}", $"LEN([{name}]) = 64 AND [{name}] NOT LIKE '%[^0-9A-F]%'"));
        }
        b.Ignore(x => x.ConfirmsPosting);
        b.HasIndex(x => new { x.EmployeeId, x.PeriodStart, x.Step }).IsUnique();
        b.HasIndex(x => new { x.ExternalReceiptKey, x.Step }).IsUnique();
        b.HasOne<Employee>().WithMany().HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<UserAccount>().WithMany().HasForeignKey(x => x.ActorUserId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<AttendancePayrollInputRevision>().WithMany()
            .HasForeignKey(x => new { x.EmployeeId, x.PeriodStart, x.SourceRevisionId })
            .HasPrincipalKey(x => new { x.EmployeeId, x.PeriodStart, x.Id }).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<AttendancePayrollAttestation>().WithMany()
            .HasForeignKey(x => new { x.EmployeeId, x.PeriodStart, x.SourceRevisionId, x.PreviousAttestationId })
            .HasPrincipalKey(x => new { x.EmployeeId, x.PeriodStart, x.SourceRevisionId, x.Id }).OnDelete(DeleteBehavior.Restrict);
    }
}
