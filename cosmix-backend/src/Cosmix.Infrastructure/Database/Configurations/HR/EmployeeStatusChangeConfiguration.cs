using Cosmix.Domain.Common.Approvals;
using Cosmix.Domain.HR.Employees;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cosmix.Infrastructure.Database.Configurations.HR;

public sealed class EmployeeStatusChangeConfiguration : IEntityTypeConfiguration<EmployeeStatusChange>
{
    public void Configure(EntityTypeBuilder<EmployeeStatusChange> builder)
    {
        builder.ToTable("EmployeeStatusChanges", "hr", table =>
        {
            table.HasCheckConstraint("CK_EmployeeStatusChanges_Status",
                "[PreviousStatus] BETWEEN 0 AND 6 AND [NewStatus] BETWEEN 0 AND 6 AND [PreviousStatus] <> [NewStatus]");
            table.HasCheckConstraint("CK_EmployeeStatusChanges_EffectiveUtc", "DATEPART(TZOFFSET, [EffectiveAt]) = 0");
        });
        builder.HasKey(x => x.Id);
        builder.HasAlternateKey(x => new { x.Id, x.EmployeeId });
        builder.HasOne(x => x.Employee).WithMany().HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ApprovalRequest>().WithMany().HasForeignKey(x => x.ApprovalRequestId)
            .IsRequired(false).OnDelete(DeleteBehavior.Restrict);
        builder.Property(x => x.Reason).HasMaxLength(1000).IsRequired();
        builder.Property(x => x.LifecyclePolicyVersion).HasMaxLength(100);
        builder.Property(x => x.ChangedByUserId).HasMaxLength(200).IsRequired();
        builder.HasIndex(x => new { x.EmployeeId, x.EffectiveAt, x.RecordedAt });
        builder.HasIndex(x => x.ApprovalRequestId).IsUnique().HasFilter("[ApprovalRequestId] IS NOT NULL");
    }
}
