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
            table.HasCheckConstraint("CK_PayrollHandoffBatches_SalaryVersion", "[CalculationVersion] IN ('adjustments-v1','salary-v2')");
            table.HasCheckConstraint("CK_PayrollHandoffBatches_SalaryState", "[ReviewState] IN ('prepared','hr_approved','rejected','cancelled','returned','locked','finance_handed_off')");
            table.HasCheckConstraint("CK_PayrollHandoffBatches_SalaryEvidence", "[CalculationVersion] <> 'salary-v2' OR ([PreviewFingerprint] IS NOT NULL AND LEN([PreviewFingerprint]) = 64 AND [FrozenFingerprint] IS NOT NULL AND LEN([FrozenFingerprint]) = 64 AND [CalculationPrivateReference] IS NOT NULL)");
            table.HasCheckConstraint("CK_PayrollHandoffBatches_SalaryApproval", "[CalculationVersion] <> 'salary-v2' OR ([ReviewState] IN ('prepared','cancelled') AND [HrApprovalRequestId] IS NULL AND [HrApprovedAt] IS NULL AND [HrApprovedByUserId] IS NULL AND [LockedAt] IS NULL) OR ([ReviewState] IN ('hr_approved','rejected','returned','locked','finance_handed_off') AND [HrApprovalRequestId] IS NOT NULL AND [HrApprovedAt] IS NOT NULL AND [HrApprovedAt] >= [PreparedAt] AND [HrApprovedByUserId] IS NOT NULL AND [HrApprovedByUserId] <> [PreparedByUserId] AND (([ReviewState] IN ('hr_approved','rejected','returned') AND [LockedAt] IS NULL) OR ([ReviewState] IN ('locked','finance_handed_off') AND [LockedAt] IS NOT NULL AND [LockedAt] >= [HrApprovedAt] AND [LockedByUserId] <> [PreparedByUserId] AND [LockedByUserId] <> [HrApprovedByUserId])))");
            table.HasCheckConstraint("CK_PayrollHandoffBatches_Return", "([ReviewState] <> 'returned' AND [ReturnApprovalRequestId] IS NULL AND [ReturnedByUserId] IS NULL AND [ReturnedAt] IS NULL) OR ([ReviewState] = 'returned' AND [CalculationVersion] = 'salary-v2' AND [ReturnApprovalRequestId] IS NOT NULL AND [ReturnedByUserId] IS NOT NULL AND [ReturnedByUserId] <> [PreparedByUserId] AND [ReturnedAt] IS NOT NULL AND [HrApprovedAt] IS NOT NULL AND [ReturnedAt] >= [HrApprovedAt] AND [LockedAt] IS NULL)");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.BatchReference).HasMaxLength(100).IsRequired();
        builder.Property(x => x.Currency).HasMaxLength(3).IsRequired();
        builder.Property(x => x.PreparedByUserId).HasMaxLength(200).IsRequired();
        builder.HasIndex(x => x.BatchReference).IsUnique();
        builder.Property(x => x.RowVersion).IsRowVersion();
        builder.Property(x => x.CalculationVersion).HasMaxLength(30).HasDefaultValue("adjustments-v1").IsRequired();
        builder.Property(x => x.PreviewFingerprint).HasMaxLength(64);
        builder.Property(x => x.FrozenFingerprint).HasMaxLength(64);
        builder.Property(x => x.CalculationPrivateReference).HasMaxLength(500);
        builder.Property(x => x.ReviewState).HasMaxLength(30).HasDefaultValue("prepared").IsRequired();
        builder.Property(x => x.HrApprovedByUserId).HasMaxLength(200);
        builder.HasOne<Cosmix.Domain.Common.Approvals.ApprovalRequest>().WithMany().HasForeignKey(x => x.HrApprovalRequestId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.ApprovalRequest).WithMany().HasForeignKey(x => x.ApprovalRequestId).OnDelete(DeleteBehavior.Restrict);
        builder.Property(x => x.ReturnedByUserId).HasMaxLength(200);
        builder.HasOne<Cosmix.Domain.Common.Approvals.ApprovalRequest>().WithMany().HasForeignKey(x => x.ReturnApprovalRequestId).OnDelete(DeleteBehavior.Restrict);
        builder.Property(x => x.LockedByUserId).HasMaxLength(200);
    }
}
