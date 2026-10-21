using Cosmix.Domain.HR.Compensation;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cosmix.Infrastructure.Database.Configurations.HR;

public sealed class CompensationRecordConfiguration : IEntityTypeConfiguration<CompensationRecord>
{
    public void Configure(EntityTypeBuilder<CompensationRecord> builder)
    {
        builder.ToTable("CompensationRecords", "hr", table =>
        {
            table.HasCheckConstraint("CK_CompensationRecords_1", "[EffectiveTo] IS NULL OR [EffectiveTo] > [EffectiveFrom]");
            table.HasCheckConstraint("CK_CompensationRecords_2", "[BaseAmount] >= 0 AND [GrossAmount] >= [BaseAmount]");
            table.HasCheckConstraint("CK_CompensationRecords_3", "[WageBasis] BETWEEN 1 AND 3");
            table.HasCheckConstraint("CK_CompensationRecords_4", "[PaymentMethod] BETWEEN 1 AND 4");
            table.HasCheckConstraint("CK_CompensationRecords_5", "[Revision] > 0");
            table.HasCheckConstraint("CK_CompensationRecords_6", "[SupersedesCompensationRecordId] IS NULL OR [SupersedesCompensationRecordId] <> [Id]");
            table.HasCheckConstraint("CK_CompensationRecords_7", "[Currency] IS NULL OR (LEN([Currency]) = 3 AND [Currency] COLLATE Latin1_General_100_BIN2 NOT LIKE '%[^A-Z]%')");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.BaseAmount).HasPrecision(18, 2);
        builder.Property(x => x.GrossAmount).HasPrecision(18, 2);
        builder.Property(x => x.Currency).HasMaxLength(3).IsRequired();
        builder.Property(x => x.RecordedByUserId).HasMaxLength(200).IsRequired();
        builder.HasOne(x => x.Employee).WithMany().HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.ApprovalRequest).WithMany().HasForeignKey(x => x.ApprovalRequestId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.SupersedesCompensationRecord).WithMany().HasForeignKey(x => new { x.SupersedesCompensationRecordId, x.EmployeeId }).HasPrincipalKey(x => new { x.Id, x.EmployeeId }).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => new { x.EmployeeId, x.EffectiveFrom, x.Revision }).IsUnique();
    }
}
