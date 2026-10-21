using Cosmix.Domain.Common.Approvals;
using Cosmix.Domain.HR.Employees;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cosmix.Infrastructure.Database.Configurations.HR;

public sealed class EmployeeChangeConfiguration : IEntityTypeConfiguration<EmployeeChange>
{
    public void Configure(EntityTypeBuilder<EmployeeChange> builder)
    {
        builder.ToTable("EmployeeChanges", "hr", table =>
        {
            table.HasCheckConstraint("CK_EmployeeChanges_Section", "[Section] IN (1, 2, 3, 4, 5, 6)");
            table.HasCheckConstraint("CK_EmployeeChanges_Approval", "[Section] = 1 OR [ApprovalRequestId] IS NOT NULL");
            table.HasCheckConstraint("CK_EmployeeChanges_Revision", "[Revision] > 0");
        });
        builder.HasKey(x => x.Id);
        builder.HasOne(x => x.Employee).WithMany().HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ApprovalRequest>().WithMany().HasForeignKey(x => x.ApprovalRequestId)
            .IsRequired(false).OnDelete(DeleteBehavior.Restrict);
        builder.Property(x => x.BeforePrivateReference).HasMaxLength(1000).IsRequired();
        builder.Property(x => x.AfterPrivateReference).HasMaxLength(1000).IsRequired();
        builder.Property(x => x.Reason).HasMaxLength(1000).IsRequired();
        builder.Property(x => x.RecordedByUserId).HasMaxLength(200).IsRequired();
        builder.HasIndex(x => new { x.EmployeeId, x.Section, x.Revision }).IsUnique();
        builder.HasIndex(x => x.ApprovalRequestId).IsUnique().HasFilter("[ApprovalRequestId] IS NOT NULL");
    }
}
