using Cosmix.Domain.HR.Allowances;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cosmix.Infrastructure.Database.Configurations.HR;

public sealed class AllowanceClaimSubmissionConfiguration : IEntityTypeConfiguration<AllowanceClaimSubmission>
{
    public void Configure(EntityTypeBuilder<AllowanceClaimSubmission> b)
    {
        b.ToTable("AllowanceClaimSubmissions", "hr", t =>
        {
            t.HasCheckConstraint("CK_AllowanceClaimSubmissions_Version", "[Version] > 0");
            t.HasCheckConstraint("CK_AllowanceClaimSubmissions_Amount", "[ClaimedAmount] >= 0");
            t.HasCheckConstraint("CK_AllowanceClaimSubmissions_Correction", "([Version] = 1 AND [CorrectionReason] IS NULL) OR ([Version] > 1 AND [CorrectionReason] IS NOT NULL AND LEN(LTRIM(RTRIM([CorrectionReason]))) > 0)");
        });
        b.HasKey(x => x.Id);
        b.HasAlternateKey(x => new { x.AllowanceClaimId, x.Version });
        b.HasIndex(x => x.ApprovalRequestId).IsUnique();
        b.Property(x => x.SnapshotPrivateReference).HasMaxLength(500).UseCollation("Latin1_General_100_BIN2").IsRequired();
        b.HasIndex(x => new { x.AllowanceClaimId, x.SnapshotPrivateReference }).IsUnique();
        b.Property(x => x.ClaimedAmount).HasPrecision(18, 2);
        b.Property(x => x.Currency).HasMaxLength(3).IsRequired();
        b.Property(x => x.CalculationPrivateReference).HasMaxLength(500).IsRequired();
        b.Property(x => x.EvidencePrivateReference).HasMaxLength(500);
        b.Property(x => x.CorrectionReason).HasMaxLength(2000);
        b.Property(x => x.SubmittedByUserId).HasMaxLength(200).IsRequired();
        b.HasOne(x => x.Claim).WithMany(x => x.Submissions).HasForeignKey(x => x.AllowanceClaimId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.ApprovalRequest).WithMany().HasForeignKey(x => x.ApprovalRequestId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.PolicyVersion).WithMany().HasForeignKey(x => x.PolicyVersionId).OnDelete(DeleteBehavior.Restrict);
    }
}
