using Cosmix.Domain.HR.PayrollHandoff;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cosmix.Infrastructure.Database.Configurations.HR;

public sealed class PayrollHandoffBatchConfiguration : IEntityTypeConfiguration<PayrollHandoffBatch>
{
    public void Configure(EntityTypeBuilder<PayrollHandoffBatch> builder)
    {
        builder.ToTable("PayrollHandoffBatches", "hr", table =>
        {
            table.HasCheckConstraint("CK_PayrollHandoffBatches_Revision", "[LinesRevision] >= 0");
            table.HasCheckConstraint("CK_PayrollHandoffBatches_1", "[PeriodEnd] >= [PeriodStart]");
            table.HasCheckConstraint("CK_PayrollHandoffBatches_2", "([LockedAt] IS NULL AND [LockedByUserId] IS NULL AND [ApprovalRequestId] IS NULL) OR ([LockedAt] IS NOT NULL AND [LockedAt] >= [PreparedAt] AND [LockedByUserId] IS NOT NULL AND [ApprovalRequestId] IS NOT NULL)");
            table.HasCheckConstraint("CK_PayrollHandoffBatches_3", "[Currency] IS NULL OR (LEN([Currency]) = 3 AND [Currency] COLLATE Latin1_General_100_BIN2 NOT LIKE '%[^A-Z]%')");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.BatchReference).HasMaxLength(100).IsRequired();
        builder.Property(x => x.Currency).HasMaxLength(3).IsRequired();
        builder.Property(x => x.PreparedByUserId).HasMaxLength(200).IsRequired();
        builder.HasIndex(x => x.BatchReference).IsUnique();
        builder.Property(x => x.RowVersion).IsRowVersion();
        builder.Ignore(x => x.CalculationVersion);
        builder.Ignore(x => x.PreviewFingerprint);
        builder.Ignore(x => x.FrozenFingerprint);
        builder.Ignore(x => x.CalculationPrivateReference);
        builder.Ignore(x => x.ReviewState);
        builder.Ignore(x => x.HrApprovalRequestId);
        builder.Ignore(x => x.HrApprovedByUserId);
        builder.Ignore(x => x.HrApprovedAt);
        builder.HasOne(x => x.ApprovalRequest).WithMany().HasForeignKey(x => x.ApprovalRequestId).OnDelete(DeleteBehavior.Restrict);
        builder.Property(x => x.LockedByUserId).HasMaxLength(200);
    }
}
