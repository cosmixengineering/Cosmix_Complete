using Cosmix.Domain.HR.PayrollHandoff;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cosmix.Infrastructure.Database.Configurations.HR;

public sealed class PayrollHandoffLineConfiguration : IEntityTypeConfiguration<PayrollHandoffLine>
{
    public void Configure(EntityTypeBuilder<PayrollHandoffLine> builder)
    {
        builder.ToTable("PayrollHandoffLines", "hr", table =>
        {
            table.HasCheckConstraint("CK_PayrollHandoffLines_1", "[Component] BETWEEN 1 AND 12");
            table.HasCheckConstraint("CK_PayrollHandoffLines_2", "[SourceVersion] > 0");
            table.HasCheckConstraint("CK_PayrollHandoffLines_3", "[Currency] IS NULL OR (LEN([Currency]) = 3 AND [Currency] COLLATE Latin1_General_100_BIN2 NOT LIKE '%[^A-Z]%')");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.SourceType).HasMaxLength(100).IsRequired();
        builder.Property(x => x.Amount).HasPrecision(18, 2);
        builder.Property(x => x.Currency).HasMaxLength(3).IsRequired();
        builder.Property(x => x.CalculationPrivateReference).HasMaxLength(500).IsRequired();
        builder.Property(x => x.TraceId).HasMaxLength(100).IsRequired();
        builder.HasOne(x => x.PayrollHandoffBatch).WithMany(x => x.Lines).HasForeignKey(x => new { x.PayrollHandoffBatchId, x.Currency }).HasPrincipalKey(x => new { x.Id, x.Currency }).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Employee).WithMany().HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.PolicyVersion).WithMany().HasForeignKey(x => x.PolicyVersionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.ApprovalRequest).WithMany().HasForeignKey(x => x.ApprovalRequestId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.AttendancePeriodLockEvent).WithMany().HasForeignKey(x => x.AttendancePeriodLockEventId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => new { x.PayrollHandoffBatchId, x.EmployeeId, x.SourceType, x.SourceId, x.SourceVersion, x.Component }).IsUnique();
        builder.HasIndex(x => x.TraceId).IsUnique();
    }
}
