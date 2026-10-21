using Cosmix.Domain.HR.Leaves;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cosmix.Infrastructure.Database.Configurations.HR;

public sealed class LeaveRequestConfiguration : IEntityTypeConfiguration<LeaveRequest>
{
    public void Configure(EntityTypeBuilder<LeaveRequest> builder)
    {
        builder.ToTable("LeaveRequests", "hr", table =>
        {
            table.HasCheckConstraint("CK_LeaveRequests_1", "[EndDate] >= [StartDate]");
            table.HasCheckConstraint("CK_LeaveRequests_2", "[RequestedMinutes] > 0");
            table.HasCheckConstraint("CK_LeaveRequests_3", "[PayTreatment] BETWEEN 0 AND 3");
            table.HasCheckConstraint("CK_LeaveRequests_4", "[RelieverEmployeeId] IS NULL OR [RelieverEmployeeId] <> [EmployeeId]");
            table.HasCheckConstraint("CK_LeaveRequests_5", "([SubmittedAt] IS NULL AND [ApprovalRequestId] IS NULL) OR ([SubmittedAt] IS NOT NULL AND [SubmittedAt] >= [CreatedAt] AND [ApprovalRequestId] IS NOT NULL)");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Reason).HasMaxLength(2000).IsRequired();
        builder.Property(x => x.ContactDuringLeave).HasMaxLength(200);
        builder.Property(x => x.HandoverNotes).HasMaxLength(2000);
        builder.Property(x => x.EvidencePrivateReference).HasMaxLength(500);
        builder.Property(x => x.RequestedByUserId).HasMaxLength(200).IsRequired();
        builder.Property(x => x.SubmissionKey).HasMaxLength(100);
        // The filtered retry key omits legacy rows; retain the unfiltered history lookup index.
        builder.HasIndex(x => x.EmployeeId);
        builder.HasIndex(x => new { x.EmployeeId, x.SubmissionKey }).IsUnique().HasFilter("[SubmissionKey] IS NOT NULL");
        builder.HasOne(x => x.PreviousRequest).WithMany().HasForeignKey(x => x.PreviousRequestId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => x.PreviousRequestId).IsUnique().HasFilter("[PreviousRequestId] IS NOT NULL");
        builder.HasOne(x => x.Employee).WithMany().HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.LeaveType).WithMany().HasForeignKey(x => x.LeaveTypeId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.RelieverEmployee).WithMany().HasForeignKey(x => x.RelieverEmployeeId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.PolicyVersion).WithMany().HasForeignKey(x => x.PolicyVersionId).OnDelete(DeleteBehavior.Restrict);
        builder.Property(x => x.RowVersion).IsRowVersion();
        builder.HasOne(x => x.ApprovalRequest).WithMany().HasForeignKey(x => x.ApprovalRequestId).OnDelete(DeleteBehavior.Restrict);
    }
}
