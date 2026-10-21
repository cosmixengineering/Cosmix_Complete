using Cosmix.Domain.HR.Policies;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cosmix.Infrastructure.Database.Configurations.HR;

public sealed class PolicyAssignmentConfiguration : IEntityTypeConfiguration<PolicyAssignment>
{
    public void Configure(EntityTypeBuilder<PolicyAssignment> builder)
    {
        builder.ToTable("PolicyAssignments", "hr", table =>
        {
            table.HasCheckConstraint("CK_PolicyAssignments_1", "[EffectiveTo] IS NULL OR [EffectiveTo] > [EffectiveFrom]");
            table.HasCheckConstraint("CK_PolicyAssignments_2", "(CASE WHEN [RoleId] IS NULL THEN 0 ELSE 1 END + CASE WHEN [EmployeeId] IS NULL THEN 0 ELSE 1 END + CASE WHEN [DepartmentId] IS NULL THEN 0 ELSE 1 END + CASE WHEN [StaffCategoryId] IS NULL THEN 0 ELSE 1 END + CASE WHEN [SiteId] IS NULL THEN 0 ELSE 1 END) = 1");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Authority).HasConversion<int>().HasDefaultValue(PolicyAuthority.ApprovedWorkflow).HasSentinel(PolicyAuthority.ApprovedWorkflow);
        builder.ToTable(t => t.HasCheckConstraint("CK_PolicyAssignment_Authority", "([Authority] = 1 AND [ApprovalRequestId] IS NOT NULL) OR ([Authority] = 2 AND [ApprovalRequestId] IS NULL)"));
        builder.Property(x => x.Reason).HasMaxLength(1000).IsRequired();
        builder.Property(x => x.InstructionPrivateReference).HasMaxLength(500);
        builder.Property(x => x.RecordedByUserId).HasMaxLength(200).IsRequired();
        builder.HasOne(x => x.PolicyVersion).WithMany().HasForeignKey(x => x.PolicyVersionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Role).WithMany().HasForeignKey(x => x.RoleId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Employee).WithMany().HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Department).WithMany().HasForeignKey(x => x.DepartmentId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.StaffCategory).WithMany().HasForeignKey(x => x.StaffCategoryId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Site).WithMany().HasForeignKey(x => x.SiteId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.ApprovalRequest).WithMany().HasForeignKey(x => x.ApprovalRequestId).OnDelete(DeleteBehavior.Restrict);
    }
}
