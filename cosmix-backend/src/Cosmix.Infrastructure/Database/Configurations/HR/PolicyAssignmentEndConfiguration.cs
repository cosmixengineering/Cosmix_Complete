using Cosmix.Domain.HR.Policies;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cosmix.Infrastructure.Database.Configurations.HR;

public sealed class PolicyAssignmentEndConfiguration : IEntityTypeConfiguration<PolicyAssignmentEnd>
{
    public void Configure(EntityTypeBuilder<PolicyAssignmentEnd> builder)
    {
        builder.ToTable("PolicyAssignmentEnds", "hr", table =>
            table.HasCheckConstraint("CK_PolicyAssignmentEnds_Reason", "LEN(LTRIM(RTRIM([Reason]))) > 0"));
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Authority).HasConversion<int>().HasDefaultValue(PolicyAuthority.ApprovedWorkflow).HasSentinel(PolicyAuthority.ApprovedWorkflow);
        builder.ToTable(t => t.HasCheckConstraint("CK_PolicyAssignmentEnd_Authority", "([Authority] = 1 AND [ApprovalRequestId] IS NOT NULL) OR ([Authority] = 2 AND [ApprovalRequestId] IS NULL)"));
        builder.Property(x => x.Reason).HasMaxLength(1000).IsRequired();
        builder.Property(x => x.RecordedByUserId).HasMaxLength(200).IsRequired();
        builder.HasOne(x => x.PolicyAssignment).WithOne().HasForeignKey<PolicyAssignmentEnd>(x => x.PolicyAssignmentId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => x.PolicyAssignmentId).IsUnique();
        builder.HasOne(x => x.ApprovalRequest).WithMany().HasForeignKey(x => x.ApprovalRequestId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
