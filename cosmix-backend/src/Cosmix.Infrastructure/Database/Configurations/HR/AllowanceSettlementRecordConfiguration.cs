using Cosmix.Domain.HR.Allowances;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cosmix.Infrastructure.Database.Configurations.HR;

public sealed class AllowanceSettlementRecordConfiguration : IEntityTypeConfiguration<AllowanceSettlementRecord>
{
    public void Configure(EntityTypeBuilder<AllowanceSettlementRecord> builder)
    {
        builder.ToTable("AllowanceSettlementRecords", "hr", table =>
        {
            table.HasCheckConstraint("CK_AllowanceSettlementRecords_1", "[State] BETWEEN 0 AND 2");
            table.HasCheckConstraint("CK_AllowanceSettlementRecords_2", "[State] <> 1 OR [PayrollHandoffLineId] IS NOT NULL");
            table.HasCheckConstraint("CK_AllowanceSettlementRecords_3", "[State] <> 2 OR [ExternalPaymentReference] IS NOT NULL");
            table.HasCheckConstraint("CK_AllowanceSettlementRecords_Submission", "[SubmissionVersion] > 0 AND [State] BETWEEN 1 AND 2");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.ExternalPaymentReference).HasMaxLength(150);
        builder.Property(x => x.IdempotencyKey).HasMaxLength(100).IsRequired();
        builder.Property(x => x.RecordedByUserId).HasMaxLength(200).IsRequired();
        builder.HasOne(x => x.AllowanceClaim).WithMany().HasForeignKey(x => x.AllowanceClaimId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.PayrollHandoffLine).WithMany().HasForeignKey(x => x.PayrollHandoffLineId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => new { x.AllowanceClaimId, x.IdempotencyKey }).IsUnique();
        builder.HasOne(x => x.Submission).WithMany().HasForeignKey(x => new { x.AllowanceClaimId, x.SubmissionVersion })
            .HasPrincipalKey(x => new { x.AllowanceClaimId, x.Version }).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => new { x.AllowanceClaimId, x.State }).IsUnique().HasFilter("[State] > 0");
    }
}
