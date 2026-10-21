using Cosmix.Domain.HR.Leaves;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cosmix.Infrastructure.Database.Configurations.HR;

public sealed class LeaveCancellationRequestConfiguration : IEntityTypeConfiguration<LeaveCancellationRequest>
{
    public void Configure(EntityTypeBuilder<LeaveCancellationRequest> builder)
    {
        builder.ToTable("LeaveCancellationRequests", "hr", table =>
        {
            table.HasCheckConstraint("CK_LeaveCancellationRequests_1", "([SubmittedAt] IS NULL AND [ApprovalRequestId] IS NULL) OR ([SubmittedAt] IS NOT NULL AND [SubmittedAt] >= [CreatedAt] AND [ApprovalRequestId] IS NOT NULL)");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.RequestedByUserId).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Reason).HasMaxLength(2000).IsRequired();
        builder.HasOne(x => x.LeaveRequest).WithMany().HasForeignKey(x => x.LeaveRequestId).OnDelete(DeleteBehavior.Restrict);
        builder.Property(x => x.RowVersion).IsRowVersion();
        builder.HasOne(x => x.ApprovalRequest).WithMany().HasForeignKey(x => x.ApprovalRequestId).OnDelete(DeleteBehavior.Restrict);
    }
}
