using Cosmix.Domain.HR.Leaves;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cosmix.Infrastructure.Database.Configurations.HR;

public sealed class LeaveEncashmentRequestConfiguration : IEntityTypeConfiguration<LeaveEncashmentRequest>
{
    public void Configure(EntityTypeBuilder<LeaveEncashmentRequest> builder)
    {
        builder.ToTable("LeaveEncashmentRequests", "hr", table =>
        {
            table.HasCheckConstraint("CK_LeaveEncashmentRequests_1", "[LeaveYearEnd] > [LeaveYearStart]");
            table.HasCheckConstraint("CK_LeaveEncashmentRequests_2", "[RequestedMinutes] > 0");
            table.HasCheckConstraint("CK_LeaveEncashmentRequests_3", "[AssessedKpiPercent] BETWEEN 0 AND 100");
            table.HasCheckConstraint("CK_LeaveEncashmentRequests_4", "[ProposedAmount] >= 0");
            table.HasCheckConstraint("CK_LeaveEncashmentRequests_5", "([SubmittedAt] IS NULL AND [ApprovalRequestId] IS NULL) OR ([SubmittedAt] IS NOT NULL AND [SubmittedAt] >= [CreatedAt] AND [ApprovalRequestId] IS NOT NULL)");
            table.HasCheckConstraint("CK_LeaveEncashmentRequests_6", "[Currency] IS NULL OR (LEN([Currency]) = 3 AND [Currency] COLLATE Latin1_General_100_BIN2 NOT LIKE '%[^A-Z]%')");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.AssessedKpiPercent).HasPrecision(9, 4);
        builder.Property(x => x.ProposedAmount).HasPrecision(18, 2);
        builder.Property(x => x.Currency).HasMaxLength(3).IsRequired();
        builder.Property(x => x.CalculationPrivateReference).HasMaxLength(500).IsRequired();
        builder.Property(x => x.RequestedByUserId).HasMaxLength(200).IsRequired();
        builder.HasOne(x => x.Employee).WithMany().HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.PolicyVersion).WithMany().HasForeignKey(x => x.PolicyVersionId).OnDelete(DeleteBehavior.Restrict);
        builder.Property(x => x.RowVersion).IsRowVersion();
        builder.HasOne(x => x.ApprovalRequest).WithMany().HasForeignKey(x => x.ApprovalRequestId).OnDelete(DeleteBehavior.Restrict);
    }
}
