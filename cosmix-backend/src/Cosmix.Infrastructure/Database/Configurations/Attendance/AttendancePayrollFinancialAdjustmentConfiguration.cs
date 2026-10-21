using Cosmix.Domain.Attendance.PayrollInputs;
using Cosmix.Domain.Authentication;
using Cosmix.Domain.HR.Employees;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cosmix.Infrastructure.Database.Configurations.Attendance;

public sealed class AttendancePayrollFinancialAdjustmentConfiguration : IEntityTypeConfiguration<AttendancePayrollFinancialAdjustment>
{
    public void Configure(EntityTypeBuilder<AttendancePayrollFinancialAdjustment> b)
    {
        AttendanceMapping.Record(b, "AttendancePayrollFinancialAdjustments",
            ("Months", "DAY([SourceMonth]) = 1 AND YEAR([SourceMonth]) BETWEEN 1900 AND 9998 AND DAY([TargetMonth]) = 1 AND YEAR([TargetMonth]) BETWEEN 1900 AND 9998 AND [TargetMonth] > [SourceMonth]"),
            ("Revision", "([Revision] = 1 AND [PreviousAdjustmentId] IS NULL) OR ([Revision] > 1 AND [PreviousAdjustmentId] IS NOT NULL)"));
        b.Property(x => x.ProtectedEvidenceReference).HasMaxLength(500);
        foreach (var name in new[] { "PreviewFingerprint", "EvidenceFingerprint" })
        {
            b.Property<string>(name).HasMaxLength(64).IsFixedLength().IsUnicode(false).UseCollation("Latin1_General_100_BIN2");
            b.ToTable("AttendancePayrollFinancialAdjustments", "attendance", t =>
                t.HasCheckConstraint($"CK_AttendancePayrollFinancialAdjustments_{name}", $"LEN([{name}]) = 64 AND [{name}] NOT LIKE '%[^0-9A-F]%'") );
        }
        b.HasIndex(x => new { x.EmployeeId, x.SourceMonth, x.Revision }).IsUnique();
        b.HasIndex(x => x.PreviousAdjustmentId).IsUnique().HasFilter("[PreviousAdjustmentId] IS NOT NULL");
        b.HasIndex(x => new { x.TargetMonth, x.EmployeeId });
        b.HasOne<Employee>().WithMany().HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<UserAccount>().WithMany().HasForeignKey(x => x.ActorUserId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<AttendancePayrollInputRevision>().WithMany()
            .HasForeignKey(x => new { x.EmployeeId, x.SourceMonth, x.SourceRevisionId })
            .HasPrincipalKey(x => new { x.EmployeeId, x.PeriodStart, x.Id }).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<AttendancePayrollInputRevision>().WithMany()
            .HasForeignKey(x => new { x.EmployeeId, x.TargetMonth, x.TargetRevisionId })
            .HasPrincipalKey(x => new { x.EmployeeId, x.PeriodStart, x.Id }).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<AttendancePayrollAttestation>().WithMany()
            .HasForeignKey(x => new { x.EmployeeId, x.SourceMonth, x.SourceRevisionId, x.ConfirmedPostingId })
            .HasPrincipalKey(x => new { x.EmployeeId, x.PeriodStart, x.SourceRevisionId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<AttendancePayrollFinancialAdjustment>().WithMany()
            .HasForeignKey(x => new { x.EmployeeId, x.SourceMonth, x.SourceRevisionId, x.PreviousAdjustmentId })
            .HasPrincipalKey(x => new { x.EmployeeId, x.SourceMonth, x.SourceRevisionId, x.Id }).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class AttendancePayrollFinancialResolutionConfiguration : IEntityTypeConfiguration<AttendancePayrollFinancialResolution>
{
    public void Configure(EntityTypeBuilder<AttendancePayrollFinancialResolution> b)
    {
        AttendanceMapping.Record(b, "AttendancePayrollFinancialResolutions",
            ("Status", "([Status] = 'posted' AND [ExternalReceiptKey] IS NOT NULL) OR ([Status] = 'cancelled' AND [ExternalReceiptKey] IS NULL AND [OccurredAtUtc] = [CreatedAtUtc])"),
            ("Time", "[OccurredAtUtc] <= [CreatedAtUtc]"));
        b.Property(x => x.Status).HasMaxLength(16).IsUnicode(false).UseCollation("Latin1_General_100_BIN2");
        b.Property(x => x.ProtectedEvidenceReference).HasMaxLength(500);
        foreach (var name in new[] { "ExternalReceiptKey", "EvidenceFingerprint" })
        {
            b.Property<string>(name).HasMaxLength(64).IsFixedLength().IsUnicode(false).UseCollation("Latin1_General_100_BIN2");
            b.ToTable("AttendancePayrollFinancialResolutions", "attendance", t =>
                t.HasCheckConstraint($"CK_AttendancePayrollFinancialResolutions_{name}", $"[{name}] IS NULL OR (LEN([{name}]) = 64 AND [{name}] NOT LIKE '%[^0-9A-F]%')"));
        }
        b.HasIndex(x => x.AdjustmentId).IsUnique();
        b.HasIndex(x => x.ExternalReceiptKey).IsUnique().HasFilter("[ExternalReceiptKey] IS NOT NULL");
        b.HasOne<AttendancePayrollFinancialAdjustment>().WithMany().HasForeignKey(x => x.AdjustmentId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<UserAccount>().WithMany().HasForeignKey(x => x.ActorUserId).OnDelete(DeleteBehavior.Restrict);
    }
}
