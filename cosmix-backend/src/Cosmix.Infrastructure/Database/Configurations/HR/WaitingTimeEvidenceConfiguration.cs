using Cosmix.Domain.HR.PolicyEffects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cosmix.Infrastructure.Database.Configurations.HR;

public sealed class WaitingTimeEvidenceConfiguration : IEntityTypeConfiguration<WaitingTimeEvidence>
{
    public void Configure(EntityTypeBuilder<WaitingTimeEvidence> builder)
    {
        builder.ToTable("WaitingTimeEvidences", "hr", table =>
        {
            table.HasCheckConstraint("CK_WaitingTimeEvidences_1", "[WaitingEndedAt] > [WaitingStartedAt]");
            table.HasCheckConstraint("CK_WaitingTimeEvidences_2", "[VerifiedWaitingMinutes] >= 0 AND [VerifiedWaitingMinutes] <= DATEDIFF(MINUTE, [WaitingStartedAt], [WaitingEndedAt])");
            table.HasCheckConstraint("CK_WaitingTimeEvidences_3", "[EmployeeId] <> [DelayedByEmployeeId]");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.WorkPairReference).HasMaxLength(150).IsRequired();
        builder.Property(x => x.EvidencePrivateReference).HasMaxLength(500).IsRequired();
        builder.Property(x => x.VerifiedByUserId).HasMaxLength(200).IsRequired();
        builder.HasOne(x => x.Employee).WithMany().HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.DelayedByEmployee).WithMany().HasForeignKey(x => x.DelayedByEmployeeId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Site).WithMany().HasForeignKey(x => x.SiteId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.PolicyVersion).WithMany().HasForeignKey(x => x.PolicyVersionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.ApprovalRequest).WithMany().HasForeignKey(x => x.ApprovalRequestId).OnDelete(DeleteBehavior.Restrict);
    }
}
