using Cosmix.Domain.HR.Leaves;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cosmix.Infrastructure.Database.Configurations.HR;

public sealed class LeaveRecoveryCommitmentConfiguration : IEntityTypeConfiguration<LeaveRecoveryCommitment>
{
    public void Configure(EntityTypeBuilder<LeaveRecoveryCommitment> builder)
    {
        builder.ToTable("LeaveRecoveryCommitments", "hr", table =>
        {
            table.HasCheckConstraint("CK_LeaveRecoveryCommitments_1", "[CommittedMinutes] > 0");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.RecordedByUserId).HasMaxLength(200).IsRequired();
        builder.HasOne(x => x.LeaveRequest).WithMany().HasForeignKey(x => x.LeaveRequestId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.PolicyVersion).WithMany().HasForeignKey(x => x.PolicyVersionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.ApprovalRequest).WithMany().HasForeignKey(x => x.ApprovalRequestId).OnDelete(DeleteBehavior.Restrict);
    }
}
