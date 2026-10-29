using Cosmix.Domain.HR.PayrollAccounting;
using Cosmix.Domain.HR.PayrollHandoff;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cosmix.Infrastructure.Database.Configurations.Finance;

public sealed class PayrollCostPlanConfiguration : IEntityTypeConfiguration<PayrollCostPlan>
{
    public void Configure(EntityTypeBuilder<PayrollCostPlan> builder)
    {
        builder.ToTable("PayrollCostPlans", "finance", table => {
            table.HasCheckConstraint("CK_PayrollCostPlan_Revision", "[Revision] > 0");
            table.HasCheckConstraint("CK_PayrollCostPlan_Decision", "([State] = 'Draft' AND [DecidedBy] IS NULL AND [DecidedAt] IS NULL AND [DecisionProofReference] IS NULL AND [DecidedByEmployeeId] IS NULL) OR ([State] IN ('Approved','Rejected') AND [DecidedBy] IS NOT NULL AND [DecidedBy] <> [CreatedBy] AND [DecidedByEmployeeId] IS NOT NULL AND [DecidedByEmployeeId] <> [CreatedByEmployeeId] AND [DecidedAt] IS NOT NULL AND [DecidedAt] >= [CreatedAt] AND [DecisionProofReference] IS NOT NULL)");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.SourceFingerprint).HasMaxLength(64).IsRequired();
        builder.Property(x => x.ProofReference).HasMaxLength(1000).IsRequired();
        builder.Property(x => x.DecisionProofReference).HasMaxLength(1000);
        builder.Property(x => x.CreatedBy).HasMaxLength(200).IsRequired();
        builder.Property(x => x.DecidedBy).HasMaxLength(200);
        builder.Property(x => x.State).HasMaxLength(20).IsRequired();
        builder.Property(x => x.RowVersion).IsRowVersion();
        builder.HasIndex(x => new { x.BatchId, x.Revision }).IsUnique();
        builder.HasIndex(x => x.BatchId).IsUnique().HasFilter("[State] = 'Draft'");
        builder.HasOne<PayrollHandoffBatch>().WithMany().HasForeignKey(x => x.BatchId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<PayrollCostPlan>().WithMany().HasForeignKey(x => x.SupersedesId).OnDelete(DeleteBehavior.Restrict);
    }
}
